﻿using System;
using Microsoft.AspNetCore.Http;

namespace Greatbone.Core
{
    ///
    /// The encapsulation of a web request/response exchange context, designed able to live across threads to support long polling.
    ///
    public class WebContext : IDisposable
    {
        // the underlying implementation
        private readonly HttpContext _impl;

        private readonly WebRequest _request;

        private readonly WebResponse _response;

        internal WebContext(HttpContext impl)
        {
            _impl = impl;
            _request = new WebRequest(impl.Request);
            _response = new WebResponse(impl.Response);
        }


        public ISession Session => _impl.Session;

        public WebRequest Request => _request;

        public WebResponse Response => _response;

        public WebSub Control { get; internal set; }

        public WebAction Action { get; internal set; }

        public IZone Zone { get; internal set; }

        public IToken Token { get; internal set; }

        public void Dispose()
        {
        }
    }
}