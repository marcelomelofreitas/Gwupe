﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gwupe.Cloud.Messaging.Response
{
    public class FaultReportRs : API.Response
    {
        public override string type {
            get { return "ReportFault-RS"; }
            set { }
        }
    }
}
