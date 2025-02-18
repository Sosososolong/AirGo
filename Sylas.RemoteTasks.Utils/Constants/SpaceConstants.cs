namespace Sylas.RemoteTasks.Utils.Constants
{
    /// <summary>
    /// 空格常量
    /// </summary>
    public static class SpaceConstants
    {
        /// <summary>
        /// 一个空格字节
        /// </summary>
        public const char OneSpace = ' ';
        /// <summary>
        /// 一个空格字符串
        /// </summary>
        public const string OneSpaceStr = " ";
        /// <summary>
        /// 两个空格
        /// </summary>
        public static readonly string TwoSpaces = new(OneSpace, 2);
        /// <summary>
        /// 一个Tab对应的空格(4个空格)
        /// </summary>
        public static readonly string OneTabSpaces = new(OneSpace, 4);
        /// <summary>
        /// 2个Tab对应的空格(8个空格)
        /// </summary>
        public static readonly string TwoTabsSpaces = new(OneSpace, 8);
        /// <summary>
        /// 3个Tab对应的空格(12个空格)
        /// </summary>
        public static readonly string ThreeTabsSpaces = new(OneSpace, 12);
        /// <summary>
        /// 4个Tab对应的空格(16个空格)
        /// </summary>
        public static readonly string FourTabsSpaces = new(OneSpace, 16);
        /// <summary>
        /// 5个Tab对应的空格(20个空格)
        /// </summary>
        public static readonly string FiveTabsSpaces = new(OneSpace, 20);
        /// <summary>
        /// 6个Tab对应的空格(24个空格)
        /// </summary>
        public static readonly string SixTabsSpaces = new(OneSpace, 24);
        /// <summary>
        /// 7个Tab对应的空格(28个空格)
        /// </summary>
        public static readonly string SevenTabsSpaces = new(OneSpace, 28);
    }
}
