using AuthorizeNet.Api.Controllers.Bases;
using AuthorizeNet.Api.Contracts.V1;
using AuthorizeNet.Api.Controllers;
using Microsoft.AspNet.Identity;
using System.Threading.Tasks;
using WebShop.Erp.Models;
using WebShop.Services;
using System.Xml.Linq;
using System.Web.Mvc;
using WebShop.Models;
using System.Net;
using System;

namespace WebShop.Controllers
{
	[Authorize]
	public class CheckoutController : Controller
	{
		protected readonly IShoppingCartService shoppingCartService;
		protected readonly ICatalogService catalogService;
		protected readonly IFinanceService financeService;
		protected readonly IErpService erpService;
		protected readonly ApplicationUserManager userManager;

		public CheckoutController(IShoppingCartService shoppingCartService, ICatalogService catalogService, IFinanceService financeService, IErpService erpService, ApplicationUserManager userManager)
		{
			this.shoppingCartService = shoppingCartService;
			this.catalogService = catalogService;
			this.financeService = financeService;
			this.erpService = erpService;
			this.userManager = userManager;
		}

		public async Task<ActionResult> Index()
		{
			Cart cart = await shoppingCartService.GetCartAsync();

			await shoppingCartService.FillCartAsync(cart, catalogService, financeService);
			return View(new CheckoutIndexViewModel() { Cart = cart });
		}


        /// <summary>
        ///     This process takes order details and card details and passes them onto the payment method. 
        /// </summary>
        /// <returns></returns>
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> Checkout()
		{
            decimal totalCost = 0;
			Cart cart = await shoppingCartService.GetCartAsync();

			ApplicationUser user = userManager.FindById(User.Identity.GetUserId());

			Order order = new Order()
			{
				FirstName = user.FirstName,
				LastName = user.LastName,
				Address = user.Address,
				House = user.House,
				Zip = user.Zip,
				City = user.City,
				Email = user.Email,
				Tax = financeService.Tax,
				Date = DateTime.Now
			};

			foreach(CartItem cartItem in cart.Items)
			{
				Product product = await catalogService.FindAsync(cartItem.ProductId);
				order.Items.Add(new OrderItem()
				{
					ProductId = product.Id,
					Price = product.Price,
					Quantity = cartItem.Quantity
				});

                totalCost += Convert.ToDecimal(product.Price * cartItem.Quantity);
			}
           
            Session["CurrentOrder"] = order;
            Session["CartTotal"] = totalCost;

            return RedirectToAction("Payment");
		}



        /// <summary>
        ///     This creates a simple thakn you page that will email the user and 
        ///         show some basic information on when the package could arrive and 
        ///         total price that was charged. 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
		public ActionResult Thanks(string id)
		{
			return View(new CheckoutThanksViewModel() { OrderId = id });
		}



        /// <summary>
        ///     This launches the payment form to get user information like shipping address
        ///         and credit card information. 
        /// </summary>
        /// <returns></returns>
        public ActionResult Payment()
        {
            return View();
        }

        /// <summary>     An Asyncronouse method to process credit card information based on
        ///                 a form the user has filled out prior.  </summary>
        [HttpPost]
        public async Task<ActionResult> AcceptPayment()
        {
            var order = Session["CurrentOrder"] as Order;
            Cart cart = await shoppingCartService.GetCartAsync();

            ///<summary>     This puts the subtotal of the entire order into a decimal 
            ///                 variable I pass into the Run method to process credit 
            ///                 cards. </summary>
            decimal subTotal = Convert.ToDecimal(Session["CartTotal"]);

            ///<summary>     The bellow strings passed into run are the values that I have 
            ///                 Been given by authorize.net to test values passeed to the 
            ///                 api. </summary>
            if(Request.Form["codButton"] == "on")
                await Run("47hFF4Udk9", "6WP4DWnD2b45yQ6m", subTotal, order, cart);

            CreateNewShipStationOrder(cart, order);

            /// <summary>     This saves the values to the order, and also removes all items
            ///                 from the cart service. </summary>
            await erpService.SaveAsync(order);
            await shoppingCartService.EmptyCartAsync(cart);

            #region SMTP mail server
            /// <summary>    This gets current user information and sends an email to them confirming that thier horder
            ///                 has been properly placed and will be sending an invoice soon. It seends the email from 
            ////                 our contact source Contact@pinnacledistro.com </summary>
            //ClaimsIdentity claimsIdentity = User.Identity as ClaimsIdentity;
            //Claim   emailClaim = claimsIdentity != null ? claimsIdentity.Claims.Where(x => x.Type == ClaimTypes.Email).FirstOrDefault() : null;
            //Claim   nameClaim  = claimsIdentity != null ? claimsIdentity.Claims.Where(x => x.Type == ClaimTypes.Name).FirstOrDefault()  : null;
            //string userEmail   = emailClaim     != null ? emailClaim.Value : User.Identity.GetUserName();
            //string userName    = nameClaim      != null ? nameClaim.Value  : User.Identity.GetUserName();


            /// Currently not working smptserver email

            //var          fromAddress  = new MailAddress("contact@pinnacledistro.com", "Pinnacle Purchase"); /// purchases@pinnacledistro.com
            //const string fromPassword = "@thebestCBD1"; ///@thebestCBD1 P1nnacle@CBD


            //var toAddress  = new MailAddress(userEmail, userName);
            //string       subject    = "Your purchase with id :" + order.Id.ToString();
            //const string body       = "Your purchase order " +
            //    "for Pinnacle WholeSale has gone through and is being processed now! \n We will email you an invoice once the order has been placed.";

            //var smtp = new SmtpClient
            //{
            //    Credentials           = new NetworkCredential(fromAddress.Address, fromPassword),
            //    DeliveryMethod        = SmtpDeliveryMethod.Network,
            //    Host                  = "smtp.gmail.com",
            //    UseDefaultCredentials = false,
            //    EnableSsl             = true,
            //    Port                  = 587,
            //};
            //using (var message = new MailMessage(fromAddress, toAddress)
            //{
            //    Subject = subject,
            //    Body    = body
            //})
            //{
            //    smtp.Send(message);
            //}

            #endregion

            /// <summary>  Redirects user to the thank you for your purchase page.  </summary>
            return RedirectToAction("Thanks", new { order.Id });
        }

        /// <summary>  This process does a lot! lets start
        ///                 1. First we create an ApiOperationBase with the apiLoginId and TransactionKey given
        ///                 2. Then create a Credit Card Object to charge. 
        ///                 3. Create a billing address based on the current user information. 
        ///                 4. Creat a trancaction request to the authorize.net api. 
        ///                 5. Execute the controller and charge the card.  </summary>
        public async Task<ANetApiResponse> Run(String ApiLoginID, String ApiTransactionKey, decimal Amount, Order order, Cart cart)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            Console.WriteLine("Charge Credit Card Sample");

            ApiOperationBase<ANetApiRequest, ANetApiResponse>.RunEnvironment = AuthorizeNet.Environment.SANDBOX;


            /// @JONAS : Need to update this infromation to allow credit card processor.
            ///          so we have the ability to at least use and break the site, while 
            ///          I am going to school. 
            // define the merchant information (authentication / transaction id)
            ApiOperationBase<ANetApiRequest, ANetApiResponse>.MerchantAuthentication = new merchantAuthenticationType()
            {
                name = ApiLoginID,
                ItemElementName = ItemChoiceType.transactionKey,
                Item = ApiTransactionKey,
            };

            var creditCard = new creditCardType
            {
                ///<summary>   This requests the card number and data for the checkout form
                ///                 and enters it into the credit-card object created by 
                ///                 the authorize.net api.  </summary>
                expirationDate = Request.Form["expmonth"] + Request.Form["expyear"],
                cardNumber     = Request.Form["cardnumber"],
                cardCode       = Request.Form["cvv"]
            };

            ///<summary>
            ///     Creates a Addresstype for the order that has the passed variables
            ///         from the checkout Form
            /// </summary>
            var billingAddress = new customerAddressType
            {
                firstName = Request.Form["billName"],
                address   = Request.Form["billAdr"],
                city      = Request.Form["billCity"],
                state     = Request.Form["billState"],
                country   = Request.Form["billCountry"],
                zip       = Request.Form["billZip"]
            };
            //standard api call to retrieve response
            var paymentType = new paymentType { Item = creditCard };

            var lineItems = new lineItemType[cart.Items.Count];
            int count     = 0;

            foreach (CartItem cartItem in cart.Items)
            {
                Product product  = await catalogService.FindAsync(cartItem.ProductId);
                lineItems[count] = new lineItemType { itemId = product.Id, name = product.Name, quantity = cartItem.Quantity, unitPrice =  Convert.ToDecimal(product.Price) };
                count++;
            }

            var transactionRequest = new transactionRequestType
            {
                transactionType = transactionTypeEnum.authCaptureTransaction.ToString(),    // charge the card

                billTo          = billingAddress,
                payment         = paymentType,
                lineItems       = lineItems,
                amount          = Amount,
            };

            var request = new createTransactionRequest { transactionRequest = transactionRequest };

            // instantiate the controller that will call the service
            var controller = new createTransactionController(request);
            controller.Execute();

            // get the response from the service (errors contained if any)
            var response = controller.GetApiResponse();

            // validate response
            if (response != null)
            {
                if (response.messages.resultCode == messageTypeEnum.Ok)
                {
                    if (response.transactionResponse.messages != null)
                    {
                        Console.WriteLine("Successfully created transaction with Transaction ID: " + response.transactionResponse.transId);
                        Console.WriteLine("Response Code: " + response.transactionResponse.responseCode);
                        Console.WriteLine("Message Code: " + response.transactionResponse.messages[0].code);
                        Console.WriteLine("Description: " + response.transactionResponse.messages[0].description);
                        Console.WriteLine("Success, Auth Code : " + response.transactionResponse.authCode);
                    }
                    else
                    {
                        Console.WriteLine("Failed Transaction.");
                        if (response.transactionResponse.errors != null)
                        {
                            Console.WriteLine("Error Code: " + response.transactionResponse.errors[0].errorCode);
                            Console.WriteLine("Error message: " + response.transactionResponse.errors[0].errorText);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Failed Transaction.");
                    if (response.transactionResponse != null && response.transactionResponse.errors != null)
                    {
                        Console.WriteLine("Error Code: " + response.transactionResponse.errors[0].errorCode);
                        Console.WriteLine("Error message: " + response.transactionResponse.errors[0].errorText);
                    }
                    else
                    {
                        Console.WriteLine("Error Code: " + response.messages.message[0].code);
                        Console.WriteLine("Error message: " + response.messages.message[0].text);
                    }
                }
            }
            else
            {
                Console.WriteLine("Null Response.");
            }

            return response;
        }


        public async void CreateNewShipStationOrder(Cart cart, Order order)
        {
            string xmlPath = Server.MapPath("~/customShip.xml");
            XDocument xmlDoc = XDocument.Load(xmlPath);

            string path = Server.MapPath("~/newOrder.xml");

            ApplicationUser user = userManager.FindById(User.Identity.GetUserId());

            string orderId = "distro-" + DateTime.Now.ToShortDateString() + DateTime.Now.ToString("HH:mm:ss.ff");
            string orderNum = "distro-Sale";
            decimal totalCost = 0;
            int shippingCost = 0;
            string shippingMethod = ""; // or COD

            var shippingAddress = new customerAddressType
            {
                firstName = Request.Form["shipFName"],
                lastName  = Request.Form["shipLName"],
                address   = Request.Form["shipAdr"],
                city      = Request.Form["shipCity"],
                state     = Request.Form["shipState"],
                country   = Request.Form["shipCountry"],
                zip       = Request.Form["shipZip"]
            };

            XElement items = new XElement("Items");


            /// <summary>
            ///     This adds all cart items to the items xelement above then adds the element to the new node
            ///         being added to the web endpoint for shipStation. 
            /// </summary>
            foreach (CartItem cartItem in cart.Items)
            {
                Product product = await catalogService.FindAsync(cartItem.ProductId);
                XElement Item = new XElement("Item");
                XElement sku = new XElement("SKU", new XCData(product.Id));
                XElement name = new XElement("Name", new XCData(product.Name));
                XElement imageUrl = new XElement("ImageUrl", "");
                XElement weight = new XElement("Weight", 0);
                XElement weightUnits = new XElement("WeightUnits", "Ounces");
                XElement quant = new XElement("Quantity", cartItem.Quantity);
                XElement price = new XElement("UnitPrice", product.Price);
                XElement location = new XElement("Location", "");

                Item.Add(sku);
                Item.Add(name);
                Item.Add(imageUrl);
                Item.Add(weight);
                Item.Add(weightUnits);
                Item.Add(quant);
                Item.Add(price);
                Item.Add(location);

                items.Add(Item);

                totalCost += Convert.ToDecimal(product.Price * cartItem.Quantity);
            }

            XElement newOrder;

            ///<summary>
            ///     @JONAS This isn't working at all right now need to fix it up a lot! 
            /// </summary>
            if (Request.Form["paymentBool"] == "true")
            {
                if (totalCost < 500)
                    shippingCost = 9;

                newOrder = new XElement(
                    new XElement("Order",
                        new XElement("OrderID", new XCData(orderId)),
                        new XElement("OrderNumber", new XCData(orderNum)),
                        new XElement("OrderDate", new XCData(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString())),
                        new XElement("OrderStatus", "paid"),
                        new XElement("LastModified", new XCData(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString())),
                        new XElement("ShippingMethod", shippingMethod),
                        new XElement("PaymentMethod", "Credit Card"),
                        new XElement("OrderTotal", totalCost),
                        new XElement("TaxAmount", 0),
                        new XElement("ShippingAmount", shippingCost),
                        new XElement("CustomerNotes", Request.Form["shipNotes"]),
                        new XElement("InternalNotes", "Distro"),
                        new XElement("Gift", "false"),
                        new XElement("GiftMessage", ""),
                        new XElement("Customer",
                            new XElement("CustomerCode", user.Email),
                            new XElement("BillTo",
                                new XElement("Name", new XCData(user.FirstName + " " + user.LastName)),
                                new XElement("Company", ""),
                                new XElement("Phone", ""),
                                new XElement("Email", new XCData(user.Email))
                            ),
                            new XElement("ShipTo",
                                new XElement("Name", new XCData(user.FirstName + " " + user.LastName)),
                                new XElement("Company", "Pinnacle Test"),
                                new XElement("Address1", order.Address),
                                new XElement("Address2", "null"),
                                new XElement("City", order.City),
                                new XElement("State", order.City),
                                new XElement("PostalCode", order.Zip),
                                new XElement("Country", new XCData("US")),
                                new XElement("Phone", user.PhoneNumber)
                            )),
                        new XElement(items)
                        )
                    );
            }
            else
            {
                newOrder = new XElement(
                    new XElement("Order",
                        new XElement("OrderID", new XCData(orderId)),
                        new XElement("OrderNumber", new XCData(orderNum)),
                        new XElement("OrderDate", new XCData(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString())),
                        new XElement("OrderStatus", "paid"),
                        new XElement("LastModified", new XCData(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString())),
                        new XElement("ShippingMethod", shippingMethod),
                        new XElement("PaymentMethod", "Credit Card"),
                        new XElement("OrderTotal", totalCost),
                        new XElement("TaxAmount", 0),
                        new XElement("ShippingAmount", shippingCost),
                        new XElement("CustomerNotes", Request.Form["shipNotes"]),
                        new XElement("InternalNotes", "Distro"),
                        new XElement("Gift", "false"),
                        new XElement("GiftMessage", ""),
                        new XElement("Customer",
                            new XElement("CustomerCode", user.Email),
                            new XElement("BillTo",
                                new XElement("Name", new XCData(user.FirstName + " " + user.LastName)),
                                new XElement("Company", ""),
                                new XElement("Phone", ""),
                                new XElement("Email", new XCData(user.Email))
                            ),
                            new XElement("ShipTo",
                                new XElement("Name", new XCData(user.FirstName + " " + user.LastName)),
                                new XElement("Company", "Pinnacle Test"),
                                new XElement("Address1", order.Address),
                                new XElement("Address2", "null"),
                                new XElement("City", order.City),
                                new XElement("State", order.City),
                                new XElement("PostalCode", order.Zip),
                                new XElement("Country", new XCData("US")),
                                new XElement("Phone", user.PhoneNumber)
                            )),
                        new XElement(items)
                        )
                    );
            }

            xmlDoc.Root.Add(newOrder);
            xmlDoc.Save(xmlPath);
        }
    }
}