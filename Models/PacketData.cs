using System;

namespace Models
{
    public class PacketData
    {
        public string Symbol { get; set; }

        public Char BuySellIndicator { get; set; }

        public int Quantity { get; set; }

        public int Price { get; set; }
        public int Sequence { get; set; }

    }
}
