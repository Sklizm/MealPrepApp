using System.Drawing;
using System.Windows.Forms;
namespace MealPrepApp

{
    /// <summary>
    /// Central colour palette — Chinese Retro theme
    /// #8c9657 moss  |  #2e2820 dark  |  #f0e5c7 cream  |  #e8ddb8 cream2
    /// </summary>
    public static class AppColors
    {
        public static readonly Color Moss       = ColorTranslator.FromHtml("#8c9657");
        public static readonly Color MossDark   = ColorTranslator.FromHtml("#6b7340");
        public static readonly Color MossLight  = ColorTranslator.FromHtml("#b5be88");
        public static readonly Color Dark       = ColorTranslator.FromHtml("#2e2820");
        public static readonly Color Cream      = ColorTranslator.FromHtml("#f0e5c7");
        public static readonly Color Cream2     = ColorTranslator.FromHtml("#e8ddb8");
        public static readonly Color Sidebar    = ColorTranslator.FromHtml("#ddd5b0");
        public static readonly Color SearchBg   = ColorTranslator.FromHtml("#9aa070");
        public static readonly Color Card1      = ColorTranslator.FromHtml("#b5be88");
        public static readonly Color Card2      = ColorTranslator.FromHtml("#8c9657");
        public static readonly Color Card3      = ColorTranslator.FromHtml("#6b7340");
        public static readonly Color TableAlt   = ColorTranslator.FromHtml("#e8e4cc");
        public static readonly Color TableSel   = ColorTranslator.FromHtml("#c8d4a0");
        public static readonly Color MealB      = ColorTranslator.FromHtml("#b5d4b5");
        public static readonly Color MealL      = ColorTranslator.FromHtml("#a8c4e0");
        public static readonly Color MealD      = ColorTranslator.FromHtml("#e0b8a8");
        public static readonly Color MealS      = ColorTranslator.FromHtml("#e8d89a");

        public static readonly Font FontTitle   = new Font("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font FontMenu    = new Font("Segoe UI", 11f);
        public static readonly Font FontAction  = new Font("Segoe UI", 11f, FontStyle.Bold);
        public static readonly Font FontBody    = new Font("Segoe UI", 10f);
        public static readonly Font FontSmall   = new Font("Segoe UI", 9f);
        public static readonly Font FontHeader  = new Font("Segoe UI", 9.5f, FontStyle.Bold);
    }
}
