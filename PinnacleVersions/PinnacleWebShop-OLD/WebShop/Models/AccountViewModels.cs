﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WebShop.Models
{
    public class ExternalLoginConfirmationViewModel
    {
        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class ExternalLoginListViewModel
    {
        public string ReturnUrl { get; set; }
    }

    public class SendCodeViewModel
    {
        public string SelectedProvider { get; set; }
        public ICollection<System.Web.Mvc.SelectListItem> Providers { get; set; }
        public string ReturnUrl { get; set; }
        public bool RememberMe { get; set; }
    }

    public class VerifyCodeViewModel
    {
        [Required]
        public string Provider { get; set; }

        [Required]
        [Display(Name = "Code")]
        public string Code { get; set; }
        public string ReturnUrl { get; set; }

        [Display(Name = "Remember this browser?")]
        public bool RememberBrowser { get; set; }

        public bool RememberMe { get; set; }
    }

    public class ForgotViewModel
    {
        [Required]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }

    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email")]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
		[Required]
		[Display(Name = "Title")]
		public Title? Title { get; set; }

		[Required]
		[StringLength(50)]
		[DataType(DataType.Text)]
		[Display(Name = "First Name")]
		public string FirstName { get; set; }

		[Required]
		[StringLength(50)]
		[DataType(DataType.Text)]
		[Display(Name = "Last Name")]
		public string LastName { get; set; }

		[Required]
		[StringLength(50)]
		[DataType(DataType.Text)]
		[Display(Name = "Address")]
		public string Address { get; set; }

		[Required]
		[StringLength(10)]
		[DataType(DataType.Text)]
		[Display(Name = "Zip")]
		public string Zip { get; set; }

		[Required]
		[StringLength(50)]
		[DataType(DataType.Text)]
		[Display(Name = "City")]
		public string City { get; set; }

        [Required]
        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "State")]
        public string State { get; set; }

        [Required]
        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "Country")]
        public string Country { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "EIN")]
        public string EIN { get; set; }

        [Required]
        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "PhoneNumber")]
        public string PhoneNumber { get; set; }

        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "Website")]
        public string Website { get; set; }

        [Required]
        [StringLength(50)]
        [DataType(DataType.Text)]
        [Display(Name = "Company")]
        public string Company { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        public string Code { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
    }
}
