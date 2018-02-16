﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Primitives;
using static Greatbone.Core.DataUtility;

namespace Greatbone.Core
{
    ///
    /// The encapsulation of a web request/response exchange context. It supports multiplexity occuring in SSE and WebSocket.
    ///
    public class WebContext : HttpContext, IDisposable
    {
        readonly IFeatureCollection features;

        private readonly DefaultConnectionInfo connection;

        readonly WebRequest request;

        readonly WebResponse response;

        internal WebContext(IFeatureCollection features)
        {
            this.features = features;
            connection = new DefaultConnectionInfo(features);
            this.request = new WebRequest(this);
            this.response = new WebResponse(this);
        }

        public override IFeatureCollection Features => features;

        public override HttpRequest Request => request;

        public override HttpResponse Response => response;

        public override ConnectionInfo Connection => connection;

        public override WebSocketManager WebSockets { get; }

        [Obsolete("This is obsolete and will be removed in a future version. The recommended alternative is to use Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions. See https://go.microsoft.com/fwlink/?linkid=845470.")]
        public override AuthenticationManager Authentication => null;

        public override ClaimsPrincipal User { get; set; } = null;

        public override IDictionary<object, object> Items { get; set; }

        public override IServiceProvider RequestServices { get; set; }

        public override CancellationToken RequestAborted { get; set; }

        public override string TraceIdentifier { get; set; } = null;

        public override ISession Session { get; set; } = null;

        public override void Abort()
        {
            features.Get<IHttpRequestLifetimeFeature>().Abort();
        }


        //
        // OBJECT PROVIDER

        object[] registry;

        int size;

        public void Register(object value)
        {
            if (registry == null)
            {
                registry = new object[8];
            }

            registry[size++] = value;
        }

        public void Register(params object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                Register(values[i]);
            }
        }

        public T Obtain<T>() where T : class
        {
            if (registry != null)
            {
                for (int i = 0; i < size; i++)
                {
                    if (registry[i] is T v) return v;
                }
            }

            return Service.Obtain<T>();
        }

        public Service Service { get; internal set; }

        /// Whether this is requested from a cluster member.
        ///
        public bool Cluster { get; internal set; }

        public Work Work { get; internal set; }

        public ProcedureDescript Procedure { get; internal set; }

        public int Subscript { get; internal set; }

        /// The decrypted/decoded principal object.
        ///
        public IData Principal { get; set; }

        /// A token string.
        ///
        internal string Token { get; }

        // levels of keys along the URI path
        Seg[] chain;

        int level; // actual number of segments

        internal void Chain(Work work, string key, object princi = null)
        {
            if (chain == null)
            {
                chain = new Seg[8];
            }

            chain[level++] = new Seg(work, key, princi);
        }

        public Seg this[int position] => position < 0 ? chain[level + position - 1] : chain[position];

        public Seg this[Type typ]
        {
            get
            {
                for (int i = 0; i < level; i++)
                {
                    Seg seg = chain[i];
                    if (seg.Work.IsOf(typ)) return seg;
                }

                return default;
            }
        }

        public Seg this[Work work]
        {
            get
            {
                for (int i = 0; i < level; i++)
                {
                    Seg seg = chain[i];
                    if (seg.Work == work) return seg;
                }

                return default;
            }
        }

        //
        // REQUEST
        //

        public string Method => Request.Method;

        public bool GET => "GET".Equals(Request.Method);

        public bool POST => "POST".Equals(Request.Method);

        string ua;

        public string Ua => ua ?? (ua = Header("User-Agent"));

        string raddr;

        public string RemoteAddr => raddr ?? (raddr = Features.Get<IHttpConnectionFeature>().RemoteIpAddress.ToString());

        public bool ByBrowser => Ua?.StartsWith("Mozilla") ?? false;

        public bool ByBrowse => ByBrowser && Header("X-Requested-With") == null;

        public bool ByWeiXin => Ua?.Contains("MicroMessenger/") ?? false;

        public bool ByJQuery => Header("X-Requested-With") != null;

        string path;

        public string Path => path ?? (path = Features.Get<IHttpRequestFeature>().Path);

        string uri;

        public string Uri => uri ?? (uri = string.IsNullOrEmpty(QueryString) ? Path : Path + QueryString);

        string url;

        public string Url => url ?? (url = Features.Get<IHttpRequestFeature>().Scheme + "://" + Header("Host" + Features.Get<IHttpRequestFeature>().RawTarget));

        string querystr;

        public string QueryString => querystr ?? (querystr = Features.Get<IHttpRequestFeature>().QueryString);

        // URL query 
        Form query;

        public Form Query => query ?? (query = new FormParser(QueryString).Parse());

        public void AddParam(string name, string value)
        {
            string q = QueryString;
            if (string.IsNullOrEmpty(q))
            {
                querystr = "?" + name + "=" + value;
                query = null; // reset parsed form
            }
            else
            {
                querystr = querystr + "&" + name + "=" + value;
                Query.Add(name, value);
            }
        }

        //
        // HEADER
        //

        public string Header(string name)
        {
            if (Request.Headers.TryGetValue(name, out var vs))
            {
                return vs;
            }

            return null;
        }

        public int? HeaderInt(string name)
        {
            if (Request.Headers.TryGetValue(name, out var vs))
            {
                string str = vs;
                if (int.TryParse(str, out var v))
                {
                    return v;
                }
            }

            return null;
        }

        public long? HeaderLong(string name)
        {
            if (Request.Headers.TryGetValue(name, out var vs))
            {
                string str = vs;
                if (long.TryParse(str, out var v))
                {
                    return v;
                }
            }

            return null;
        }

        public DateTime? HeaderDateTime(string name)
        {
            if (Request.Headers.TryGetValue(name, out var vs))
            {
                string str = vs;
                if (StrUtility.TryParseUtcDate(str, out var v))
                {
                    return v;
                }
            }

            return null;
        }

        public string[] Headers(string name)
        {
            if (Request.Headers.TryGetValue(name, out var vs))
            {
                return vs;
            }

            return null;
        }

        public IRequestCookieCollection Cookies => Request.Cookies;

        // request body
        byte[] buffer;

        int count = -1;

        // request entity (ArraySegment<byte>, JObj, JArr, Form, XElem, null)
        object entity;

        public async Task<ArraySegment<byte>> ReadAsync()
        {
            if (count == -1) // if not yet read
            {
                count = 0;
                int? clen = HeaderInt("Content-Length");
                if (clen > 0)
                {
                    // reading
                    int len = (int) clen;
                    buffer = BufferUtility.GetByteBuffer(len); // borrow from the pool
                    while ((count += await Request.Body.ReadAsync(buffer, count, (len - count))) < len)
                    {
                    }
                }
            }

            return new ArraySegment<byte>(buffer, 0, count);
        }

        public async Task<M> ReadAsync<M>() where M : class, ISource
        {
            if (entity == null && count == -1) // if not yet parse and read
            {
                // read
                count = 0;
                int? clen = HeaderInt("Content-Length");
                if (clen > 0)
                {
                    int len = (int) clen;
                    buffer = BufferUtility.GetByteBuffer(len); // borrow from the pool
                    while ((count += await Request.Body.ReadAsync(buffer, count, (len - count))) < len)
                    {
                    }
                }

                // parse
                string ctyp = Header("Content-Type");
                entity = ParseContent(ctyp, buffer, count, typeof(M));
            }

            return entity as M;
        }

        public async Task<D> ReadObjectAsync<D>(byte proj = 0x0f, D obj = default) where D : IData, new()
        {
            if (entity == null && count == -1) // if not yet parse and read
            {
                // read
                count = 0;
                int? clen = HeaderInt("Content-Length");
                if (clen > 0)
                {
                    int len = (int) clen;
                    buffer = BufferUtility.GetByteBuffer(len); // borrow from the pool
                    while ((count += await Request.Body.ReadAsync(buffer, count, (len - count))) < len)
                    {
                    }
                }

                // parse
                string ctyp = Header("Content-Type");
                entity = ParseContent(ctyp, buffer, count);
            }

            if (!(entity is ISource inp))
            {
                return default;
            }

            if (obj == null)
            {
                obj = new D();
            }

            obj.Read(inp, proj);
            return obj;
        }

        public async Task<D[]> ReadArrayAsync<D>(byte proj = 0x0f) where D : IData, new()
        {
            if (entity == null && count == -1) // if not yet parse and read
            {
                // read
                count = 0;
                int? clen = HeaderInt("Content-Length");
                if (clen > 0)
                {
                    int len = (int) clen;
                    buffer = BufferUtility.GetByteBuffer(len); // borrow from the pool
                    while ((count += await Request.Body.ReadAsync(buffer, count, (len - count))) < len)
                    {
                    }
                }

                // parse
                string ctyp = Header("Content-Type");
                entity = ParseContent(ctyp, buffer, count);
            }

            return (entity as ISource)?.ToArray<D>(proj);
        }

        //
        // RESPONSE
        //

        public void SetHeader(string name, int v)
        {
            Response.Headers.Add(name, new StringValues(v.ToString()));
        }

        public void SetHeader(string name, long v)
        {
            Response.Headers.Add(name, new StringValues(v.ToString()));
        }

        public void SetHeader(string name, string v)
        {
            Response.Headers.Add(name, new StringValues(v));
        }

        public void SetHeaderAbsent(string name, string v)
        {
            IHeaderDictionary headers = Response.Headers;
            if (!headers.TryGetValue(name, out _))
            {
                headers.Add(name, new StringValues(v));
            }
        }

        public void SetHeader(string name, DateTime v)
        {
            string str = StrUtility.FormatUtcDate(v);
            Response.Headers.Add(name, new StringValues(str));
        }

        public void SetHeader(string name, params string[] values)
        {
            Response.Headers.Add(name, new StringValues(values));
        }

        public void SetTokenCookie<P>(P prin, byte proj, int maxage = 0) where P : class, IData, new()
        {
            ((Service<P>) Service).SetTokenCookie(this, prin, proj, maxage);
        }

        public bool InCache { get; internal set; }

        public int Status
        {
            get => Response.StatusCode;
            set => Response.StatusCode = value;
        }

        public IContent Content { get; internal set; }

        // public, no-cache or private
        public bool? Public { get; internal set; }

        /// the cached response is to be considered stale after its age is greater than the specified number of seconds.
        public int MaxAge { get; internal set; }

        public void Give(int status, IContent cont = null, bool? @public = null, int maxage = 60)
        {
            Status = status;
            Content = cont;
            Public = @public;
            MaxAge = maxage;
        }

        public void Give(int status, ISource inp, bool? @public = null, int maxage = 60)
        {
            Status = status;
            Content = inp.Dump();
            Public = @public;
            MaxAge = maxage;
        }

        public void Give(int status, string text, bool? @public = null, int maxage = 60)
        {
            StrContent cont = new StrContent(true);
            cont.Add(text);

            // set response states
            Status = status;
            Content = cont;
            Public = @public;
            MaxAge = maxage;
        }

        public void Give(int status, IData obj, byte proj = 0x0f, bool? pub = null, int maxage = 60)
        {
            JsonContent cont = new JsonContent(true).Put(null, obj, proj);
            Status = status;
            Content = cont;
            Public = pub;
            MaxAge = maxage;
        }

        public void Give<D>(int status, D[] arr, byte proj = 0x0f, bool? pub = null, int maxage = 60) where D : IData
        {
            JsonContent cont = new JsonContent(true).Put(null, arr, proj);
            Status = status;
            Content = cont;
            Public = pub;
            MaxAge = maxage;
        }

        internal async Task SendAsync()
        {
            // set connection header if absent
            SetHeaderAbsent("Connection", "keep-alive");

            // cache control header
            if (Public.HasValue)
            {
                string hv = (Public.Value ? "public" : "private") + ", max-age=" + MaxAge;
                SetHeader("Cache-Control", hv);
            }

            // content check
            if (Content == null) return;

            // deal with not modified situations by etag
            string etag = Content.ETag;
            if (etag != null)
            {
                string inm = Header("If-None-Match");
                if (etag == inm)
                {
                    Status = 304; // not modified
                    return;
                }

                SetHeader("ETag", etag);
            }

            // static content special deal
            if (Content is StaticContent sta)
            {
                DateTime? since = HeaderDateTime("If-Modified-Since");
                Debug.Assert(sta != null);
                if (since != null && sta.Modified <= since)
                {
                    Status = 304; // not modified
                    return;
                }

                DateTime? last = sta.Modified;
                if (last != null)
                {
                    SetHeader("Last-Modified", StrUtility.FormatUtcDate(last.Value));
                }

                if (sta.GZip)
                {
                    SetHeader("Content-Encoding", "gzip");
                }
            }

            // send out the content async
            Response.ContentLength = Content.Size;
            Response.ContentType = Content.Type;
            await Response.Body.WriteAsync(Content.ByteBuffer, 0, Content.Size);
        }

        public void Dispose()
        {
            // request content buffer
            if (buffer != null)
            {
                BufferUtility.Return(buffer);
            }

            // pool returning
            if (!InCache)
            {
                if (Content is DynamicContent dcont)
                {
                    BufferUtility.Return(dcont.ByteBuffer);
                }
            }
        }
    }
}