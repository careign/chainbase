﻿using Greatbone.Core;

namespace Greatbone.Sample
{
    /// <summary>
    /// The content service.
    /// </summary>
    public class ContService : AbsService
    {
        public ContService(WebConfig cfg) : base(cfg)
        {
            AddSub<PostModule>("post", false);
            
            AddSub<NoticeModule>("notice", false);
        }

    }
    
}