using System;
using System.Threading.Tasks;
using Greatbone;
using static Greatbone.Modal;
using static Core.User;

namespace Core
{
    public abstract class ItemVarWork : Work
    {
        const int PICAGE = 60 * 60;

        protected ItemVarWork(WorkConfig cfg) : base(cfg)
        {
        }

        public void icon(WebContext wc)
        {
            string orgid = wc[typeof(IOrgVar)];
            string name = wc[this];
            using (var dc = NewDbContext())
            {
                if (dc.Query1("SELECT icon FROM items WHERE orgid = @1 AND name = @2", p => p.Set(orgid).Set(name)))
                {
                    dc.Let(out ArraySegment<byte> byteas);
                    if (byteas.Count == 0) wc.Give(204); // no content 
                    else wc.Give(200, new StaticContent(byteas), true, PICAGE);
                }
                else wc.Give(404, @public: true, maxage: PICAGE); // not found
            }
        }

        public void img(WebContext wc, int ordinal)
        {
            string orgid = wc[-1];
            string name = wc[this];
            using (var dc = NewDbContext())
            {
                if (dc.Query1("SELECT img" + ordinal + " FROM items WHERE orgid = @1 AND name = @2", p => p.Set(orgid).Set(name)))
                {
                    dc.Let(out ArraySegment<byte> byteas);
                    if (byteas.Count == 0) wc.Give(204); // no content 
                    else wc.Give(200, new StaticContent(byteas), true, PICAGE);
                }
                else wc.Give(404, @public: true, maxage: PICAGE); // not found
            }
        }
    }

    public class CoreItemVarWork : ItemVarWork
    {
        public CoreItemVarWork(WorkConfig cfg) : base(cfg)
        {
        }

        [Ui("购买"), Tool(ButtonOpen), Item('A')]
        public async Task buy(WebContext wc)
        {
            string orgid = wc[-1];
            var org = Obtain<Map<string, Org>>()[orgid];
            User prin = (User) wc.Principal;
            string itemname = wc[this];
            string name, city, a, b, tel; // form values
            short num;
            if (wc.GET)
            {
                wc.GivePane(200, h =>
                {
                    using (var dc = NewDbContext())
                    {
                        h.FORM_();
                        if (dc.Scalar("SELECT 1 FROM orders WHERE wx = @1 AND status = 0 AND orgid = @2", p => p.Set(prin.wx).Set(orgid)) == null) // to create new
                        {
                            // show addr inputs for order creation
                            h.FIELDSET_("填写收货信息");
                            name = prin.name;
                            city = prin.city;
                            a = prin.addr;
                            tel = prin.tel;
                            h.FIELD_("地址").SELECT(nameof(city), city, City.All, required: true).TEXT(nameof(a), a, max: 20, required: true)._FIELD();
                            h.TEXT(nameof(name), name, "姓名", max: 4, min: 2, required: true).TEL(nameof(tel), tel, "电话", pattern: "[0-9]+", max: 11, min: 11, required: true);
                            h._FIELDSET();
                        }
                        // quantity
                        h.FIELDSET_("加入购物车");
                        dc.Sql("SELECT ").collst(Item.Empty).T(" FROM items WHERE orgid = @1 AND name = @2");
                        var it = dc.Query1<Item>(p => p.Set(orgid).Set(itemname));
                        h.FIELD_("货品").ICON("icon", width: 0x16)._T(it.name)._FIELD();
                        h.FIELD_("数量").NUMBER(nameof(num), it.min, min: it.min, step: it.step)._T(it.unit)._FIELD();
                        h._FIELDSET();

                        h.BOTTOMBAR_().BUTTON("确定")._BOTTOMBAR();
                        h._FORM();
                    }
                });
            }
            else // POST
            {
                using (var dc = NewDbContext())
                {
                    dc.Query1("SELECT unit, price FROM items WHERE orgid = @1 AND name = @2", p => p.Set(orgid).Set(itemname));
                    dc.Let(out string unit).Let(out decimal price);

                    if (dc.Query1("SELECT * FROM orders WHERE wx = @2 AND status = 0 AND orgid = @1", p => p.Set(orgid).Set(prin.wx))) // add to existing cart order
                    {
                        var o = dc.ToObject<Order>();
                        (await wc.ReadAsync<Form>()).Let(out num);
                        o.AddItem(itemname, unit, price, num);
                        o.TotalUp();
                        dc.Execute("UPDATE orders SET rev = rev + 1, items = @1, total = @2 WHERE id = @3", p => p.Set(o.items).Set(o.total).Set(o.id));
                    }
                    else // create a new order
                    {
                        var f = await wc.ReadAsync<Form>();
                        name = f[nameof(name)];
                        city = f[nameof(city)];
                        a = f[nameof(a)];
                        b = f[nameof(b)];
                        tel = f[nameof(tel)];
                        num = f[nameof(num)];
                        var o = new Order
                        {
                            rev = 1,
                            status = 0,
                            orgid = orgid,
                            orgname = org.name,
                            typ = 0, // ordinal order
                            wx = prin.wx,
                            name = name,
                            addr = a,
                            tel = tel,
                            created = DateTime.Now
                        };
                        o.AddItem(itemname, unit, price, num);
                        o.TotalUp();
                        const byte proj = 0xff ^ Order.KEY ^ Order.LATER;
                        dc.Sql("INSERT INTO orders ")._(o, proj)._VALUES_(o, proj);
                        dc.Execute(p => o.Write(p, proj));
                    }
                    wc.GivePane(200, m =>
                    {
                        m.P("商品已经成功加入购物车");
                        m.BOTTOMBAR_().A_CLOSE("继续选购", true).A("去购物车付款", "/my//order/", true, targ: "_parent")._BOTTOMBAR();
                    });
                }
            }
        }
    }

    public class OprItemVarWork : ItemVarWork
    {
        public OprItemVarWork(WorkConfig cfg) : base(cfg)
        {
        }

        [Ui("修改"), Tool(ButtonShow, 2), User(OPRSTAFF)]
        public async Task basic(WebContext wc)
        {
            string orgid = wc[-2];
            string name = wc[this];
            if (wc.GET)
            {
                using (var dc = NewDbContext())
                {
                    var o = dc.Query1<Item>("SELECT * FROM items WHERE orgid = @1 AND name = @2", p => p.Set(orgid).Set(name));
                    wc.GivePane(200, h =>
                    {
                        h.FORM_();
                        h.FIELDSET_("填写货品信息");
                        h.STATIC(o.name, "名称");
                        h.TEXTAREA(nameof(o.descr), o.descr, "描述", min: 20, max: 50, required: true);
                        h.TEXT(nameof(o.unit), o.unit, "单位", required: true);
                        h.NUMBER(nameof(o.price), o.price, "单价", required: true);
                        h.NUMBER(nameof(o.min), o.min, "起订", min: (short) 1);
                        h.NUMBER(nameof(o.step), o.step, "增减", min: (short) 1);
                        h.SELECT(nameof(o.status), o.status, Item.Statuses, "状态");
                        h.NUMBER(nameof(o.stock), o.stock, "可供");
                        h._FIELDSET();
                        h._FORM();
                    });
                }
            }
            else // POST
            {
                const byte proj = 0xff ^ Item.PK;
                var o = await wc.ReadObjectAsync<Item>(proj);
                using (var dc = NewDbContext())
                {
                    dc.Sql("UPDATE items")._SET_(Item.Empty, proj).T(" WHERE orgid = @1 AND name = @2");
                    dc.Execute(p =>
                    {
                        o.Write(p, proj);
                        p.Set(orgid).Set(name);
                    });
                }
                wc.GivePane(200); // close
            }
        }

        [Ui("照片"), Tool(ButtonCrop), User(OPRSTAFF)]
        public new async Task icon(WebContext wc)
        {
            string orgid = wc[-2];
            string name = wc[this];
            if (wc.GET)
            {
                using (var dc = NewDbContext())
                {
                    if (dc.Query1("SELECT icon FROM items WHERE orgid = @1 AND name = @2", p => p.Set(orgid).Set(name)))
                    {
                        dc.Let(out ArraySegment<byte> byteas);
                        if (byteas.Count == 0) wc.Give(204); // no content 
                        else wc.Give(200, new StaticContent(byteas));
                    }
                    else wc.Give(404); // not found           
                }
            }
            else // POST
            {
                var f = await wc.ReadAsync<Form>();
                ArraySegment<byte> jpeg = f[nameof(jpeg)];
                using (var dc = NewDbContext())
                {
                    if (dc.Execute("UPDATE items SET icon = @1 WHERE orgid = @2 AND name = @3", p => p.Set(jpeg).Set(orgid).Set(name)) > 0)
                    {
                        wc.Give(200); // ok
                    }
                    else wc.Give(500); // internal server error
                }
            }
        }
    }
}