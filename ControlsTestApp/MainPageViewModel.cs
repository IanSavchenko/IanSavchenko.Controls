using System.Collections.Generic;
using System.Linq;

namespace ControlsTestApp
{
    internal class MainPageViewModel
    {
        public List<string> TestItems
        {
            get { return Enumerable.Range(0, 100).Select(t => t.ToString()).ToList(); }
        }
             
    }
}