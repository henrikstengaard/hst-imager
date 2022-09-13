namespace HstWbInstaller.Imager.Core.Models
{
    using System;

    public class Size
    {
        public readonly long Value;
        public readonly Unit Unit;

        public Size() : this(0, Unit.Bytes)
        {
        }

        public Size(long value, Unit unit)
        {
            if (unit == Unit.Percent && (value < 0 || value > 100))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Percent value must be between 0-100%");
            }
            
            Value = value;
            Unit = unit;
        }

        public override string ToString()
        {
            return $"{Value}{(Unit == Unit.Percent ? "%" : Unit)}";
        }
    }
}