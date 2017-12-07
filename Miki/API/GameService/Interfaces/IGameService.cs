﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miki.API.GameService
{
	interface IGameService
	{
		Task<IGameResponse> Play<T>( T args ) where T : IGameArguments;
	}
}