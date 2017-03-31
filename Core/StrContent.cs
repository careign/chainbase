namespace Greatbone.Core
{
    public class StrContent : DynamicContent
    {
        public StrContent(bool sendable, bool pooled = false, int capacity = 256) : base(sendable, pooled, capacity)
        {
        }

        public override string Type => "text/plain";
    }
}