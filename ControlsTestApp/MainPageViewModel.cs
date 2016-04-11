using System.Collections.Generic;
using System.Linq;

namespace ControlsTestApp
{
    internal class MainPageViewModel
    {
        public List<string> TestItems
        {
            get { return Enumerable.Range(0, 10).Select(t => t.ToString("D2")).ToList(); }
        }

        public List<string> TestItemsShort
        {
            get { return Enumerable.Range(0, 2).Select(t => t.ToString("D2")).ToList(); }
        }
    }
}