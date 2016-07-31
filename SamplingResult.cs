using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LocContract;
namespace SamplingManager
{
    public class SamplingResult
    {
        public int samplingPointId;
        public TagType bindingTagType;
        public string bindingTagsMacStr;
        public DateTime startTime;
        public DateTime endTime;
        public Dictionary<string, SamplingStaticsInfo> staticsInfoDic;
        public SamplingResult()
        {
            this.staticsInfoDic = new Dictionary<string, SamplingStaticsInfo>();
        }
    }
}
