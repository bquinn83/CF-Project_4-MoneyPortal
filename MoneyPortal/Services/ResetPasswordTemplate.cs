﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MoneyPortal.Services
{
    public class PasswordResetTemplate : IMailTemplate
    {
        public string CodeUrl { get; set; }
    }
}