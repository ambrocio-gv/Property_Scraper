﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StageTwoPointB.Models
{
    public class Proxy
    {
        public int? Id { get; internal set; }
        public string? ProxyIP { get; internal set; }
        public string? Port { get; internal set; }
    }
}