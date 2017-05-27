﻿using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Greatbone.Core;

namespace Greatbone.Sample
{
    public static class WeiXinUtility
    {
        public const string WXAUTH = "wxauth";

        public const string BODY_DESC = "粗粮达人-健康产品";

        public const string ADDR = "http://shop.144000.tv";


        static string appid;

        static string appsecret;

        static string mchid;

        static string noncestr;

        static string key;


        static Client WCPay;

        static readonly Client WeiXin = new Client("https://api.weixin.qq.com");

        static volatile string AccessToken;

        private static bool stop;

        private static readonly Thread Renewer = new Thread(async () =>
        {
            while (!stop)
            {
                JObj jo = await WeiXin.GetAsync<JObj>(null, "/cgi-bin/token?grant_type=client_credential&appid=" + appid + "&secret=" + appsecret);
                string access_token = jo?[nameof(access_token)];
                AccessToken = access_token;

                // suspend for 1 hour
                Thread.Sleep(3600000);
            }
        });

        public static void Setup(string weixinfile, string p12file, bool deploy)
        {
            // load weixin parameters
            var wx = DataInputUtility.FileTo<JObj>(weixinfile);
            appid = wx[nameof(appid)];
            appsecret = wx[nameof(appsecret)];
            mchid = wx[nameof(mchid)];
            noncestr = wx[nameof(noncestr)];
            key = wx[nameof(key)];

            // load and init client certificate
            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual
            };
            X509Certificate2 cert = new X509Certificate2(p12file, mchid, X509KeyStorageFlags.MachineKeySet);
            handler.ClientCertificates.Add(cert);
            WCPay = new Client(handler)
            {
                BaseAddress = new Uri("https://api.mch.weixin.qq.com")
            };

            // start the access token renewer thread
            if (deploy)
            {
                Renewer.Start();
            }
        }

        public static void Stop()
        {
            stop = true;
        }

        public static void GiveRedirectWeiXinAuthorize(this ActionContext ac)
        {
            string redirect_url = WebUtility.UrlEncode(ADDR + ac.Uri);
            ac.SetHeader("Location", "https://open.weixin.qq.com/connect/oauth2/authorize?appid=" + appid + "&redirect_uri=" + redirect_url + "&response_type=code&scope=snsapi_userinfo&state=" + WXAUTH + "#wechat_redirect");
            ac.Give(303);
        }

        public static async Task<Accessor> GetAccessorAsync(string code)
        {
            string url = "/sns/oauth2/access_token?appid=" + appid + "&secret=" + appsecret + "&code=" + code + "&grant_type=authorization_code";
            JObj jo = await WeiXin.GetAsync<JObj>(null, url);
            if (jo == null) return default(Accessor);

            string access_token = jo[nameof(access_token)];
            if (access_token == null)
            {
                return default(Accessor);
            }
            string openid = jo[nameof(openid)];

            return new Accessor(access_token, openid);
        }

        public struct Accessor
        {
            internal readonly string access_token;

            internal readonly string openid;

            internal Accessor(string access_token, string openid)
            {
                this.access_token = access_token;
                this.openid = openid;
            }
        }

        public static async Task<User> GetUserInfoAsync(string access_token, string openid)
        {
            JObj jo = await WeiXin.GetAsync<JObj>(null, "/sns/userinfo?access_token=" + access_token + "&openid=" + openid + "&lang=zh_CN");
            string nickname = jo[nameof(nickname)];
            string city = jo[nameof(city)];
            return new User {wx = openid, name = nickname, city = city};
        }

        static readonly DateTime EPOCH = new DateTime(1970, 1, 1);

        public static IContent BuildPrepayContent(string prepay_id)
        {
            string package = "prepay_id=" + prepay_id;
            string timeStamp = ((int) (DateTime.Now - EPOCH).TotalSeconds).ToString();

            JObj jo = new JObj
            {
                new JMbr("appId", appid),
                new JMbr("nonceStr", noncestr),
                new JMbr("package", package),
                new JMbr("signType", "MD5"),
                new JMbr("timeStamp", timeStamp),
            };
            jo.Add("paySign", Sign(jo, "paySign"));

            return jo.Dump();
        }

        public static async Task PostTransferAsync()
        {
            XElem x = new XElem("xml");
            x.AddChild("mch_appid", appid);
            x.AddChild("mchid", appid);
            x.AddChild("nonce_str", appid);
            x.AddChild("partner_trade_no", appid);
            x.AddChild("openid", appid);
            x.AddChild("check_name", appid);
            x.AddChild("re_user_name", appid);
            x.AddChild("amount", appid);
            x.AddChild("desc", appid);
            x.AddChild("spbill_create_ip", appid);

            string sign = x.Child(nameof(sign));
            x.AddChild("sign", sign);

            XElem resp = (await WCPay.PostAsync<XElem>(null, "/mmpaymkttransfers/promotion/transfers", x.Dump())).Y;
        }

        public static async Task<string> PostUnifiedOrderAsync(long orderid, decimal total, string openid, string ip, string notifyurl)
        {
            XElem x = new XElem("xml");
            x.AddChild("appid", appid);
            x.AddChild("body", BODY_DESC);
            x.AddChild("mch_id", mchid);
            x.AddChild("nonce_str", noncestr);
            x.AddChild("notify_url", notifyurl);
            x.AddChild("openid", openid);
            x.AddChild("out_trade_no", orderid.ToString());
            x.AddChild("spbill_create_ip", ip);
            x.AddChild("total_fee", ((int) (total * 100)).ToString());
            x.AddChild("trade_type", "JSAPI");
            string sign = Sign(x);
            x.AddChild("sign", sign);

            XElem xe = (await WCPay.PostAsync<XElem>(null, "/pay/unifiedorder", x.Dump())).Y;
            string prepay_id = xe.Child(nameof(prepay_id));

            return prepay_id;
        }

        public static bool Notified(XElem xe, out long out_trade_no, out decimal cash)
        {
            cash = 0;
            out_trade_no = 0;

            string appid = xe.Child(nameof(appid));
            string mch_id = xe.Child(nameof(mch_id));
            string nonce_str = xe.Child(nameof(nonce_str));

            if (appid != WeiXinUtility.appid || mch_id != mchid || nonce_str != noncestr) return false;

            string result_code = xe.Child(nameof(result_code));

            if (result_code != "SUCCESS") return false;

            string sign = xe.Child(nameof(sign));
            xe.Sort();
            if (sign != Sign(xe, "sign")) return false;

            int cash_fee = xe.Child(nameof(cash_fee)); // in cent
            cash = ((decimal) cash_fee) / 100;
            out_trade_no = xe.Child(nameof(out_trade_no)); // 商户订单号
            return true;
        }

        public static async Task<decimal> PostOrderQueryAsync(long orderid)
        {
            XElem x = new XElem("xml");
            x.AddChild("appid", appid);
            x.AddChild("mch_id", mchid);
            x.AddChild("nonce_str", noncestr);
            x.AddChild("out_trade_no", orderid.ToString());
            string sign = Sign(x);
            x.AddChild("sign", sign);

            XElem xe = (await WCPay.PostAsync<XElem>(null, "/pay/orderquery", x.Dump())).Y;

            sign = xe.Child(nameof(sign));
            xe.Sort();
            if (sign != Sign(xe, "sign")) return 0;

            string return_code = xe.Child(nameof(return_code));
            if (return_code != "SUCCESS") return 0;

            decimal cash_fee = xe.Child(nameof(cash_fee));

            return cash_fee;
        }

        public static async Task<bool> PostRefundAsync(long orderid, decimal total, decimal cash)
        {
            XElem x = new XElem("xml");
            x.AddChild("appid", appid);
            x.AddChild("mch_id", mchid);
            x.AddChild("nonce_str", noncestr);
            x.AddChild("op_user_id", mchid);
            x.AddChild("out_refund_no", orderid.ToString());
            x.AddChild("out_trade_no", orderid.ToString());
            x.AddChild("refund_fee", ((int) (cash * 100)).ToString());
            x.AddChild("total_fee", ((int) (total * 100)).ToString());
            string sign = Sign(x);
            x.AddChild("sign", sign);

            XElem xe = (await WCPay.PostAsync<XElem>(null, "/secapi/pay/refund", x.Dump())).Y;

            sign = xe.Child(nameof(sign));
            xe.Sort();
            if (sign != Sign(xe, "sign")) return false;

            string return_code = xe.Child(nameof(return_code));
            if (return_code != "SUCCESS") return false;

            decimal cash_fee = xe.Child(nameof(cash_fee));

            return true;
        }

        public static async Task<bool> PostRefundQueryAsync(long orderid)
        {
            XElem x = new XElem("xml");
            x.AddChild("appid", appid);
            x.AddChild("mch_id", mchid);
            x.AddChild("nonce_str", noncestr);
            x.AddChild("op_user_id", mchid);
            x.AddChild("out_refund_no", orderid.ToString());
            x.AddChild("out_trade_no", orderid.ToString());
            string sign = Sign(x);
            x.AddChild("sign", sign);

            XElem xe = (await WCPay.PostAsync<XElem>(null, "/pay/refundquery", x.Dump())).Y;

            sign = xe.Child(nameof(sign));
            xe.Sort();
            if (sign != Sign(xe, "sign")) return false;

            string return_code = xe.Child(nameof(return_code));
            if (return_code != "SUCCESS") return false;

            decimal cash_fee = xe.Child(nameof(cash_fee));

            return true;
        }

        static string Sign(XElem xe, string exclude = null)
        {
            StringBuilder sb = new StringBuilder(1024);
            for (int i = 0; i < xe.Count; i++)
            {
                XElem child = xe.Child(i);

                // not include the sign field
                if (exclude != null && child.Tag == exclude) continue;

                if (sb.Length > 0)
                {
                    sb.Append('&');
                }
                sb.Append(child.Tag).Append('=').Append(child.Text);
            }

            sb.Append("&key=").Append(key);

            return StrUtility.MD5(sb.ToString());
        }

        static string Sign(JObj jo, string exclude = null)
        {
            StringBuilder sb = new StringBuilder(1024);
            for (int i = 0; i < jo.Count; i++)
            {
                JMbr mbr = jo[i];

                // not include the sign field
                if (exclude != null && mbr.Name == exclude) continue;

                if (sb.Length > 0)
                {
                    sb.Append('&');
                }
                sb.Append(mbr.Name).Append('=').Append((string) mbr);
            }

            sb.Append("&key=").Append(key);

            return StrUtility.MD5(sb.ToString());
        }
    }
}