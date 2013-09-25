﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using BlitsMe.Cloud.Messaging.Elements;

namespace BlitsMe.Cloud.Messaging.Request
{
    [DataContract]
    public class SubscribeRq : API.Request
    {
        public override string type { get { return "Subscribe-RQ"; } set { } }
        [DataMember] public String username;
        [DataMember] public bool subscribe;
        [DataMember] public UserElement userElement;
    }
}
