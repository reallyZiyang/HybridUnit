using System;

namespace Game.Play.Base.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class OrderAttribute : Attribute
    {
        public int Order { get; }

        public OrderAttribute(int order)
        {
            Order = order;
        }
    }
}