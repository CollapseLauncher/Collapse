using System.Collections.Generic;

namespace Hi3Helper.Shared.Region.Honkai
{
    public class Dispatcher
    {
        public List<RegionList> region_list { get; set; }
    }

    public class RegionList
    {
        public string dispatch_url { get; set; }
        public string name { get; set; }
        public RegionListExt ext { get; set; }
    }

    public class RegionListExt
    {
        public List<string> ex_resource_url_list { get; set; }
    }
}
