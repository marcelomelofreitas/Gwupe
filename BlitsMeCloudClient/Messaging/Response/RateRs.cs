﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gwupe.Cloud.Messaging.API;
using System.Runtime.Serialization;

namespace Gwupe.Cloud.Messaging.Response
{
    [DataContract]
    public class RateRs : API.Response
    {
        public override String type  {
            get { return "Rate-RS"; }
            set { }
        }
    }
}
