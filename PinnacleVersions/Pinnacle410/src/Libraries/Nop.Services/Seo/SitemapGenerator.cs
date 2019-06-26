using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Blogs;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Forums;
using Nop.Core.Domain.News;
using Nop.Core.Domain.Security;
using Nop.Services.Catalog;
using Nop.Services.Topics;

namespace Nop.Services.Seo
{
    /// <summary>
    /// Represents a sitemap generator
    /// </summary>
    public partial class SitemapGenerator : ISitemapGenerator
    {
        #region Fields

        private readonly BlogSettings _blogSettings;
        private readonly CommonSettings _commonSettings;
        private readonly ForumSettings _forumSettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IProductService _productService;
        private readonly IProductTagService _productTagService;
        private readonly IStoreContext _storeContext;
        private readonly ITopicService _topicService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly NewsSettings _newsSettings;
        private readonly SecuritySettings _securitySettings;

        #endregion

        #region Ctor

        public SitemapGenerator(BlogSettings blogSettings,
            CommonSettings commonSettings,
            ForumSettings forumSettings,
            IActionContextAccessor actionContextAccessor,
            ICategoryService categoryService,
            IManufacturerService manufacturerService,
            IProductService productService,
            IProductTagService productTagService,
            IStoreContext storeContext,
            ITopicService topicService,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper,
            NewsSettings newsSettings,
            SecuritySettings securitySettings)
        {
            this._blogSettings = blogSettings;
            this._commonSettings = commonSettings;
            this._forumSettings = forumSettings;
            this._actionContextAccessor = actionContextAccessor;
            this._categoryService = categoryService;
            this._manufacturerService = manufacturerService;
            this._productService = productService;
            this._productTagService = productTagService;
            this._storeContext = storeContext;
            this._topicService = topicService;
            this._urlHelperFactory = urlHelperFactory;
            this._urlRecordService = urlRecordService;
            this._webHelper = webHelper;
            this._newsSettings = newsSettings;
            this._securitySettings = securitySettings;
        }

        #endregion

        #region Nested class

        /// <summary>
        /// Represents sitemap URL entry
        /// </summary>
        protected class SitemapUrl
        {
            /// <summary>
            /// Ctor
            /// </summary>
            /// <param name="location">URL of the page</param>
            /// <param name="frequency">Update frequency</param>
            /// <param name="updatedOn">Updated on</param>
            public SitemapUrl(string location, UpdateFrequency frequency, DateTime updatedOn)
            {
                Location = location;
                UpdateFrequency = frequency;
                UpdatedOn = updatedOn;
            }

            /// <summary>
            /// Gets or sets URL of the page
            /// </summary>
            public string Location { get; set; }

            /// <summary>
            /// Gets or sets a value indicating how frequently the page is likely to change
            /// </summary>
            public UpdateFrequency UpdateFrequency { get; set; }

            /// <summary>
            /// Gets or sets the date of last modification of the file
            /// </summary>
            public DateTime UpdatedOn { get; set; }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get UrlHelper
        /// </summary>
        /// <returns>UrlHelper</returns>
        protected virtual IUrlHelper GetUrlHelper()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
        }

        /// <summary>
        /// Get HTTP protocol
        /// </summary>
        /// <returns>Protocol name as string</returns>
        protected virtual string GetHttpProtocol()
        {
            return _securitySettings.ForceSslForAllPages ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        }

        /// <summary>
        /// Generate URLs for the sitemap
        /// </summary>
        /// <returns>List of URL for the sitemap</returns>
        protected virtual IList<SitemapUrl> GenerateUrls()
        {
            var sitemapUrls = new List<SitemapUrl>();

            var urlHelper = GetUrlHelper();
            //home page
            var homePageUrl = urlHelper.RouteUrl("HomePage", null, GetHttpProtocol());
            sitemapUrls.Add(new SitemapUrl(homePageUrl, UpdateFrequency.Weekly, DateTime.UtcNow));

            //search products
            var productSearchUrl = urlHelper.RouteUrl("ProductSearch", null, GetHttpProtocol());
            sitemapUrls.Add(new SitemapUrl(productSearchUrl, UpdateFrequency.Weekly, DateTime.UtcNow));

            //contact us
            var contactUsUrl = urlHelper.RouteUrl("ContactUs", null, GetHttpProtocol());
            sitemapUrls.Add(new SitemapUrl(contactUsUrl, UpdateFrequency.Weekly, DateTime.UtcNow));

            //news
            if (_newsSettings.Enabled)
            {
                var url = urlHelper.RouteUrl("NewsArchive", null, GetHttpProtocol());
                sitemapUrls.Add(new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow));
            }

            //blog
            if (_blogSettings.Enabled)
            {
                var url = urlHelper.RouteUrl("Blog", null, GetHttpProtocol());
                sitemapUrls.Add(new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow));
            }

            //blog
            if (_forumSettings.ForumsEnabled)
            {
                var url = urlHelper.RouteUrl("Boards", null, GetHttpProtocol());
                sitemapUrls.Add(new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow));
            }

            //categories
            if (_commonSettings.SitemapIncludeCategories)
                sitemapUrls.AddRange(GetCategoryUrls());

            //manufacturers
            if (_commonSettings.SitemapIncludeManufacturers)
                sitemapUrls.AddRange(GetManufacturerUrls());

            //products
            if (_commonSettings.SitemapIncludeProducts)
                sitemapUrls.AddRange(GetProductUrls());

            //product tags
            if (_commonSettings.SitemapIncludeProductTags)
                sitemapUrls.AddRange(GetProductTagUrls());

            //topics
            sitemapUrls.AddRange(GetTopicUrls());

            //custom URLs
            sitemapUrls.AddRange(GetCustomUrls());

            return sitemapUrls;
        }

        /// <summary>
        /// Get category URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetCategoryUrls()
        {
            var urlHelper = GetUrlHelper();
            return _categoryService.GetAllCategories().Select(category =>
            {
                var url = urlHelper.RouteUrl("Category", new { SeName = _urlRecordService.GetSeName(category) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, category.UpdatedOnUtc);
            });
        }

        /// <summary>
        /// Get manufacturer URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetManufacturerUrls()
        {
            var urlHelper = GetUrlHelper();
            return _manufacturerService.GetAllManufacturers(storeId: _storeContext.CurrentStore.Id).Select(manufacturer =>
            {
                var url = urlHelper.RouteUrl("Manufacturer", new { SeName = _urlRecordService.GetSeName(manufacturer) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, manufacturer.UpdatedOnUtc);
            });
        }

        /// <summary>
        /// Get product URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetProductUrls()
        {
            var urlHelper = GetUrlHelper();
            return _productService.SearchProducts(storeId: _storeContext.CurrentStore.Id,
                visibleIndividuallyOnly: true, orderBy: ProductSortingEnum.CreatedOn).Select(product =>
                {
                    var url = urlHelper.RouteUrl("Product", new { SeName = _urlRecordService.GetSeName(product) }, GetHttpProtocol());
                    return new SitemapUrl(url, UpdateFrequency.Weekly, product.UpdatedOnUtc);
                });
        }

        /// <summary>
        /// Get product tag URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetProductTagUrls()
        {
            var urlHelper = GetUrlHelper();
            return _productTagService.GetAllProductTags().Select(productTag =>
            {
                var url = urlHelper.RouteUrl("ProductsByTag", new { SeName = _urlRecordService.GetSeName(productTag) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow);
            });
        }

        /// <summary>
        /// Get topic URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetTopicUrls()
        {
            var urlHelper = GetUrlHelper();
            return _topicService.GetAllTopics(_storeContext.CurrentStore.Id).Where(t => t.IncludeInSitemap).Select(topic =>
            {
                var url = urlHelper.RouteUrl("Topic", new { SeName = _urlRecordService.GetSeName(topic) }, GetHttpProtocol());
                return new SitemapUrl(url, UpdateFrequency.Weekly, DateTime.UtcNow);
            });
        }

        /// <summary>
        /// Get custom URLs for the sitemap
        /// </summary>
        /// <returns>Sitemap URLs</returns>
        protected virtual IEnumerable<SitemapUrl> GetCustomUrls()
        {
            var storeLocation = _webHelper.GetStoreLocation();

            return _commonSettings.SitemapCustomUrls.Select(customUrl =>
                new SitemapUrl(string.Concat(storeLocation, customUrl), UpdateFrequency.Weekly, DateTime.UtcNow));
        }

        /// <summary>
        /// Write sitemap index file into the stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="sitemapNumber">The number of sitemaps</param>
        protected virtual void WriteSitemapIndex(Stream stream, int sitemapNumber)
        {
            var urlHelper = GetUrlHelper();
            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("sitemapindex");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
                writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xsi:schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                //write URLs of all available sitemaps
                for (var id = 1; id <= sitemapNumber; id++)
                {
                    var url = urlHelper.RouteUrl("sitemap-indexed.xml", new { Id = id }, GetHttpProtocol());
                    var location = XmlHelper.XmlEncode(url);

                    writer.WriteStartElement("sitemap");
                    writer.WriteElementString("loc", location);
                    writer.WriteElementString("lastmod", DateTime.UtcNow.ToString(NopSeoDefaults.SitemapDateFormat));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }

        /// <summary>
        /// Write sitemap file into the stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="sitemapUrls">List of sitemap URLs</param>
        protected virtual void WriteSitemap(Stream stream, IList<SitemapUrl> sitemapUrls)
        {
            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.WriteStartDocument();
                writer.WriteStartElement("urlset");
                writer.WriteAttributeString("xmlns", "http://www.sitemaps.org/schemas/sitemap/0.9");
                writer.WriteAttributeString("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xsi:schemaLocation", "http://www.sitemaps.org/schemas/sitemap/0.9 http://www.sitemaps.org/schemas/sitemap/0.9/sitemap.xsd");

                //write URLs from list to the sitemap
                foreach (var url in sitemapUrls)
                {
                    writer.WriteStartElement("url");
                    var location = XmlHelper.XmlEncode(url.Location);

                    writer.WriteElementString("loc", location);
                    writer.WriteElementString("changefreq", url.UpdateFrequency.ToString().ToLowerInvariant());
                    writer.WriteElementString("lastmod", url.UpdatedOn.ToString(NopSeoDefaults.SitemapDateFormat, CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// This will build an XML sitemap for better index with search engines.
        /// See http://en.wikipedia.org/wiki/Sitemaps for more information.
        /// </summary>
        /// <param name="id">Sitemap identifier</param>
        /// <returns>Sitemap.xml as string</returns>
        public virtual string Generate(int? id)
        {
            using (var stream = new MemoryStream())
            {
                Generate(stream, id);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// This will build an XML sitemap for better index with search engines.
        /// See http://en.wikipedia.org/wiki/Sitemaps for more information.
        /// </summary>
        /// <param name="id">Sitemap identifier</param>
        /// <param name="stream">Stream of sitemap.</param>
        public virtual void Generate(Stream stream, int? id)
        {
            //generate all URLs for the sitemap
            var sitemapUrls = GenerateUrls();

            //split URLs into separate lists based on the max size 
            var sitemaps = sitemapUrls.Select((url, index) => new { Index = index, Value = url })
                .GroupBy(group => group.Index / NopSeoDefaults.SitemapMaxUrlNumber)
                    .Select(group => group.Select(url => url.Value).ToList()).ToList();

            if (!sitemaps.Any())
                return;

            if (id.HasValue)
            {
                //requested sitemap does not exist
                if (id.Value == 0 || id.Value > sitemaps.Count)
                    return;

                //otherwise write a certain numbered sitemap file into the stream
                WriteSitemap(stream, sitemaps.ElementAt(id.Value - 1));
            }
            else
            {
                //URLs more than the maximum allowable, so generate a sitemap index file
                if (sitemapUrls.Count >= NopSeoDefaults.SitemapMaxUrlNumber)
                {
                    //write a sitemap index file into the stream
                    WriteSitemapIndex(stream, sitemaps.Count);
                }
                else
                {
                    //otherwise generate a standard sitemap
                    WriteSitemap(stream, sitemaps.First());
                }
            }
        }

        #endregion
    }
}