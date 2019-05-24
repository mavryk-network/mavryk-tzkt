﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

using Tezzycat.Models;

namespace Tezzycat.Services.Protocols
{
    public class Proto2Handler : IProtocolHandler
    {
        public string Kind => "Proto2";

        public Task<AppState> ApplyBlock(JObject block)
        {
            throw new NotImplementedException();
        }

        public Task<AppState> RevertLastBlock()
        {
            throw new NotImplementedException();
        }
    }
}
