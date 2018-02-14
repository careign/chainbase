using System.Threading.Tasks;
using Greatbone.Core;
using static Greatbone.Core.Modal;

namespace Greatbone.Sample
{
    [User]
    public abstract class SlideVarWork : Work
    {
        protected SlideVarWork(WorkConfig cfg) : base(cfg)
        {
        }
    }

    public class AdmSlideVarWork : SlideVarWork
    {
        public AdmSlideVarWork(WorkConfig cfg) : base(cfg)
        {
        }

        [Ui("回复"), Tool(ButtonShow)]
        public async Task reply(WebContext ac)
        {
            string shopid = ac[typeof(ShopVarWork)];
            User prin = (User) ac.Principal;
            string wx = ac[this];

            string text = null;
            if (ac.GET)
            {
                ac.GivePane(200, m =>
                {
                    m.FORM_();
                    m.TEXT(nameof(text), text, label: "发送信息", pattern: "[\\S]*", max: 30, required: true);
                    m._FORM();
                });
            }
            else
            {
                var f = await ac.ReadAsync<Form>();
                text = f[nameof(text)];
                ac.GivePane(200);
            }
        }
    }
}