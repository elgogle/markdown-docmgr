using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.DirectoryServices;

namespace MarkdownRepository.Lib
{
    using System.DirectoryServices.AccountManagement;

    public class AdAccount
    {
        public static string GetUserNameById(string userId)
        {
            string fullName = userId;
            var docMgr = new DocumentManager();
            var username = docMgr.GetUserName(userId);
            if (!string.IsNullOrWhiteSpace(username)) return username;

            using (PrincipalContext context = new PrincipalContext(ContextType.Domain))
            {
                var a = userId.Split('\\');
                using (UserPrincipal user = UserPrincipal.FindByIdentity(context, a.Length>1?a[1]:a[0]))
                {
                    if (user != null)
                    {
                        fullName = user.DisplayName;
                        docMgr.SaveUserName(userId, fullName);
                    }
                }
            }

            return fullName;
        }

        public static bool IsAuthenticated(string domain, string userId, string pwd)
        {
            string LDAPPath = "LDAP://" + domain;
            string domainAndUsername = domain + @"\" + userId;
            DirectoryEntry entry = new DirectoryEntry(LDAPPath,
                domainAndUsername,
                pwd);
            Object obj;

            try
            {
                // Bind to the native AdsObject to force authentication.
                obj = entry.NativeObject;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                entry = null;
                obj = null;
            }
        }
    }
}