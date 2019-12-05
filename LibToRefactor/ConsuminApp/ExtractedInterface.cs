// LibToRefactor.TwoClass in ConsuminApp
namespace LibToRefactor
{
    interface TwoClass_ExtractedInterface
    {
        T PerformGeneric<T>(T inputVal); // IMethodSymbol used 1 times;
        string PerformGeneric2(System.Collections.Generic.Dictionary<string, string> inputDictionary, System.Collections.Generic.Dictionary<System.Collections.Generic.Dictionary<string, string>, string> secondParameter); // IMethodSymbol used 1 times;
        LibToRefactor.OneClass ClassValue { get; set; }  //IFieldSymbol converted to Property in interface, used 1 times;
        void TwoClass(); // IMethodSymbol used 1 times;
    }

    interface OneClass_ExtractedInterface
    {
        void OneClass(); // IMethodSymbol used 1 times;
        void OneClass(string withOneStringConstructor); // IMethodSymbol used 1 times;
        int IntProp { get; set; } // IPropertySymbol used 1 times;
    }
}