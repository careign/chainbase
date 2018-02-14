﻿using System.Threading.Tasks;

namespace Greatbone.Core
{
    /// <summary>
    /// To implement authentication behavior on a service class.
    /// </summary>
    public interface IAuthenticate
    {
        bool Authenticate(WebContext ac, bool e);
    }

    /// <summary>
    /// To implement asynchronous authentication behavior on a service class.
    /// </summary>
    public interface IAuthenticateAsync
    {
        Task<bool> AuthenticateAsync(WebContext ac, bool e);
    }
}