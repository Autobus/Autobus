﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Autobus.Delegates
{
    public delegate Task OnMessageDelegate<in TMessage>(TMessage message);
}
