using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace MarkdownRepository.Models
{
    public class LoginModel
    {
        [Required(ErrorMessage ="请输入你的域帐户")]
        [Display(Name = "域帐户", Prompt = @"CN\lwxxxx")]
        public string UserName { get; set; }

        [Required(ErrorMessage ="请输入密码")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }
    }
}