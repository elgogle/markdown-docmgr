using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MarkdownRepository.Lib
{
    using System.DirectoryServices.AccountManagement;

    public class AdAccount
    {
        public static string GetUserNameById(string userId)
        {
            string fullName = userId;
            using (PrincipalContext context = new PrincipalContext(ContextType.Domain))
            {
                using (UserPrincipal user = UserPrincipal.FindByIdentity(context, userId))
                {
                    if (user != null)
                    {
                        fullName = user.DisplayName;
                    }
                }
            }

            return fullName;
        }
    }
}