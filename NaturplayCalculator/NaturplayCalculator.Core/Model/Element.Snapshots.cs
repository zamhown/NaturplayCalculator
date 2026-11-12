using System.Collections.Generic;
using System.Linq;

namespace NaturplayCalculator.Model
{
    public partial class Element
    {
        public Element Clone()
        {
            var copy = new Element
            {
                value = value.Clone(),
                unitOperator = unitOperator,
                isMinus = isMinus,
                binOperators = new List<BinaryOperator>(binOperators),
                children = children.Select(child => child.Clone()).ToList(),
                Comments = new List<string>(Comments)
            };
            copy.valueString.Copy(valueString);
            return copy;
        }

        private static void GroupPowers(Element element)
        {
            foreach (var child in element.children) GroupPowers(child);
            if (element.binOperators.Count < 2 || element.binOperators.Any(op => op != BinaryOperator.Power)) return;
            var right = element.children[element.children.Count - 1];
            for (int i = element.children.Count - 2; i >= 1; i--)
                right = new Element(new List<Element> { element.children[i], right }, new List<BinaryOperator> { BinaryOperator.Power }, UnitaryOperator.none);
            element.children = new List<Element> { element.children[0], right };
            element.binOperators = new List<BinaryOperator> { BinaryOperator.Power };
        }
    }
}
