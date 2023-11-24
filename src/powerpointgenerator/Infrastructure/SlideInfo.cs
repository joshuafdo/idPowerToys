using Syncfusion.Presentation;

namespace IdPowerToys.PowerPointGenerator
{
    public class SlideInfo
    {
        public string PolicyName { get; set; }
        public ConditionalAccessPolicy? Policy { get; set; }
        public ISlide Slide { get; set; }
    }
}
