﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace Saltarelle.Configuration {
	public class PluginElement {
		public string Assembly { get; private set; }

	    public PluginElement(string assembly) {
	        Assembly = assembly;
	    }
	}
}
