using System;
using System.Text;

namespace Dazinator.Extensions.Http.Handlers.Authentication.Basic
{
    public static class BasicAuthenticationUtils
    {
        public static string GetAuthenticationHeaderValue(string username, string password)
        {
            return Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
        }
    }
}
