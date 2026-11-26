using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RepairConnect.Pages
{
    public class LoginModel : PageModel
    {

        [BindProperty]
        public Credential credential
        {
            get;
            set;
        }
        public void OnGet()
        {
        }

        private int coount = 0;

        public class Credential
        {
            [Required]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }
        }
    }
}
