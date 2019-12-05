using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibToRefactor
{
    public class TwoClass
    {
        OneClass c1 = new OneClass();

        public T PerformGeneric<T>(T inputVal) where T : new()
        {
            return new T();
        }

        public string PerformGeneric2(Dictionary<string, string> inputDictionary, Dictionary<Dictionary<string, string>, string> secondParameter) 
        {
            return "";
        }

        public string Value;

        public OneClass ClassValue;

        private OneClass _classBackingField;

        public OneClass ClassProp
        {
            get { return _classBackingField; }
            set { _classBackingField = value; }
        }
    }
}
