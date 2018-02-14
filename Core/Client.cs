﻿using System;
using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static Greatbone.Core.DataUtility;

namespace Greatbone.Core
{
    /// <summary>
    /// A client of RPC, service and/or event queue.
    /// </summary>
    public class Client : HttpClient, IMappable<string>
    {
        const int AHEAD = 1000 * 12;

        static readonly Uri PollUri = new Uri("*", UriKind.Relative);

        readonly Service service;

        // prepared header value
        readonly string x_event;

        // target serviceid
        readonly string peer;

        // this field is only accessed by the scheduler
        Task pollTask;

        // point of time to retry, set due to timeout or disconnection
        volatile int retryat;

        internal long evtid;

        public Client(HttpClientHandler handler) : base(handler)
        {
        }

        public Client(string raddr) : this(null, null, raddr)
        {
        }

        internal Client(Service service, string peer, string raddr)
        {
            this.service = service;
            this.peer = peer;

            Map<string, EventDoer> eds = service?.Events;
            if (eds != null)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < eds.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(eds.At(i).key);
                }

                x_event = sb.ToString();
            }

            BaseAddress = new Uri(raddr);
            Timeout = TimeSpan.FromSeconds(5);
        }

        public string Key => peer;


        public void TryPoll(int ticks)
        {
            if (ticks < retryat)
            {
                return;
            }

            if (pollTask != null && !pollTask.IsCompleted)
            {
                return;
            }

            pollTask = Task.Run(async () =>
            {
                for (;;)
                {
                    HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, PollUri);
                    HttpRequestHeaders reqhs = req.Headers;
                    reqhs.TryAddWithoutValidation("From", service.Id);

                    HttpResponseMessage rsp;
                    try
                    {
                        rsp = await SendAsync(req);
                        if (rsp.StatusCode == HttpStatusCode.NoContent)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        retryat = Environment.TickCount + AHEAD;
                        return;
                    }

                    HttpResponseHeaders rsphs = rsp.Headers;
                    byte[] cont = await rsp.Content.ReadAsByteArrayAsync();
                    EventContext ec = new EventContext(this)
                    {
                        // time = rsphs.GetValue(X_ARG)
                    };

                    // parse and process one by one
                    long id = 0;
                    // DateTime time;
                    EventDoer ei = null;
                }
            });
        }

        //
        // RPC
        //

        public async Task<byte[]> GetAsync(WebContext ac, string uri)
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (peer != null && ac != null)
                {
                    if (ac.Token != null)
                    {
                        req.Headers.Add("Authorization", "Token " + ac.Token);
                    }
                }

                HttpResponseMessage resp = await SendAsync(req, HttpCompletionOption.ResponseContentRead);
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                retryat = Environment.TickCount + AHEAD;
            }

            return null;
        }

        public async Task<M> GetAsync<M>(WebContext ac, string uri) where M : class, IDataInput
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (peer != null && ac != null)
                {
                    if (ac.Token != null)
                    {
                        req.Headers.Add("Authorization", "Token " + ac.Token);
                    }
                }

                HttpResponseMessage rsp = await SendAsync(req, HttpCompletionOption.ResponseContentRead);
                if (rsp.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                byte[] bytea = await rsp.Content.ReadAsByteArrayAsync();
                string ctyp = rsp.Content.Headers.GetValue("Content-Type");
                return (M) ParseContent(ctyp, bytea, bytea.Length, typeof(M));
            }
            catch
            {
                retryat = Environment.TickCount + AHEAD;
            }

            return null;
        }

        public async Task<D> GetObjectAsync<D>(WebContext ac, string uri, byte proj = 0x0f) where D : IData, new()
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (peer != null && ac != null)
                {
                    if (ac.Token != null)
                    {
                        req.Headers.Add("Authorization", "Token " + ac.Token);
                    }
                }

                HttpResponseMessage rsp = await SendAsync(req, HttpCompletionOption.ResponseContentRead);
                if (rsp.StatusCode != HttpStatusCode.OK)
                {
                    return default;
                }

                byte[] bytea = await rsp.Content.ReadAsByteArrayAsync();
                string ctyp = rsp.Content.Headers.GetValue("Content-Type");
                IDataInput inp = ParseContent(ctyp, bytea, bytea.Length);
                D obj = new D();
                obj.Read(inp, proj);
                return obj;
            }
            catch
            {
                retryat = Environment.TickCount + AHEAD;
            }

            return default;
        }

        public async Task<D[]> GetArrayAsync<D>(WebContext ac, string uri, byte proj = 0x0f) where D : IData, new()
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, uri);
                if (peer != null && ac != null)
                {
                    if (ac.Token != null)
                    {
                        req.Headers.Add("Authorization", "Token " + ac.Token);
                    }
                }

                HttpResponseMessage rsp = await SendAsync(req, HttpCompletionOption.ResponseContentRead);
                if (rsp.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                byte[] bytea = await rsp.Content.ReadAsByteArrayAsync();
                string ctyp = rsp.Content.Headers.GetValue("Content-Type");
                IDataInput inp = ParseContent(ctyp, bytea, bytea.Length);
                return inp.ToArray<D>(proj);
            }
            catch
            {
                retryat = Environment.TickCount + AHEAD;
            }

            return null;
        }

        public async Task<int> PostAsync(WebContext ac, string uri, IContent content)
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, uri);
                if (peer != null && ac != null)
                {
                    if (ac.Token != null)
                    {
                        req.Headers.Add("Authorization", "Token " + ac.Token);
                    }
                }

                req.Content = (HttpContent) content;
                req.Headers.TryAddWithoutValidation("Content-Type", content.Type);
                req.Headers.TryAddWithoutValidation("Content-Length", content.Size.ToString());

                HttpResponseMessage rsp = await SendAsync(req, HttpCompletionOption.ResponseContentRead);
                return (int) rsp.StatusCode;
            }
            catch
            {
                retryat = Environment.TickCount + AHEAD;
            }
            finally
            {
                if (content is DynamicContent cont)
                {
                    BufferUtility.Return(cont);
                }
            }

            return 0;
        }

        public async Task<(int code, M inp)> PostAsync<M>(WebContext ctx, string uri, IContent content) where M : class, IDataInput
        {
            try
            {
                HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, uri);
                if (ctx != null)
                {
                    req.Headers.Add("Authorization", "Token " + ctx.Token);
                }

                req.Content = (HttpContent) content;
                req.Headers.TryAddWithoutValidation("Content-Type", content.Type);
                req.Headers.TryAddWithoutValidation("Content-Length", content.Size.ToString());

                HttpResponseMessage rsp = await SendAsync(req, HttpCompletionOption.ResponseContentRead);
                string ctyp = rsp.Content.Headers.GetValue("Content-Type");
                if (ctyp == null)
                {
                    return ((int) rsp.StatusCode, null);
                }
                else
                {
                    byte[] bytes = await rsp.Content.ReadAsByteArrayAsync();
                    M inp = ParseContent(ctyp, bytes, bytes.Length, typeof(M)) as M;
                    return ((int) rsp.StatusCode, inp);
                }
            }
            catch
            {
                retryat = Environment.TickCount + AHEAD;
            }
            finally
            {
                if (content is DynamicContent cont)
                {
                    BufferUtility.Return(cont);
                }
            }

            return default((int, M));
        }
    }
}