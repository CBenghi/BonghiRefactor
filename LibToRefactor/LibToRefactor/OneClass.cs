using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibToRefactor
{
    public class OneClass
    {
        public enum OneClassEnum
        {
            One,
            Two
        }

        public void NoParameterMethod()
        {

        }

        public void OneParameterMethod(string stringName)
        {

        }

        public void NotUsedMethod()
        {
            //Just to be sure 
        }

        private int _backingInt;

        public OneClass()
        { 
        }

        public OneClass(string withOneStringConstructor)
        {
        }

        public int IntProp
        {
            get { return _backingInt; }
            set { _backingInt = value; }
        }
    }
}
