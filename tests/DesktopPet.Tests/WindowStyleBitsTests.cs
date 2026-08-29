using DesktopPet.Utils;
using Xunit;

namespace DesktopPet.Tests;

/// <summary>
/// 驗證設計檔 §2.1/§10.2 點穿的樣式位元運算：只翻目標 bit、其餘保留、冪等；
/// 開／關 <c>WS_EX_TRANSPARENT</c> 不得影響透明（<c>WS_EX_LAYERED</c>）與退出 Alt+Tab（<c>WS_EX_TOOLWINDOW</c>）。
/// </summary>
public class WindowStyleBitsTests
{
    // 對照設計檔的實際常數值，避免測試與 NativeMethods 各說各話。
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    // D1 之後的基準樣式：分層（透明）+ 工具視窗，點穿關閉。
    private const long BaseStyle = WS_EX_LAYERED | WS_EX_TOOLWINDOW;

    [Fact]
    public void Enable_sets_the_flag_without_touching_others()
    {
        long result = WindowStyleBits.Apply(BaseStyle, WS_EX_TRANSPARENT, enabled: true);

        Assert.Equal(WS_EX_TRANSPARENT, result & WS_EX_TRANSPARENT); // 已設定
        Assert.Equal(WS_EX_LAYERED, result & WS_EX_LAYERED);         // 透明保留
        Assert.Equal(WS_EX_TOOLWINDOW, result & WS_EX_TOOLWINDOW);   // 退出 Alt+Tab 保留
    }

    [Fact]
    public void Disable_clears_only_the_flag()
    {
        long on = WindowStyleBits.Apply(BaseStyle, WS_EX_TRANSPARENT, enabled: true);
        long off = WindowStyleBits.Apply(on, WS_EX_TRANSPARENT, enabled: false);

        Assert.Equal(0, off & WS_EX_TRANSPARENT); // 已清除
        Assert.Equal(BaseStyle, off);             // 回到基準（其餘位元完好）
    }

    [Fact]
    public void Enable_is_idempotent()
    {
        long once = WindowStyleBits.Apply(BaseStyle, WS_EX_TRANSPARENT, enabled: true);
        long twice = WindowStyleBits.Apply(once, WS_EX_TRANSPARENT, enabled: true);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void Disable_is_idempotent_when_already_clear()
    {
        long result = WindowStyleBits.Apply(BaseStyle, WS_EX_TRANSPARENT, enabled: false);
        Assert.Equal(BaseStyle, result); // 本就沒有該 bit → 不變
    }

    [Fact]
    public void High_bit_flag_clears_without_sign_extension()
    {
        // 旗標若為高位（bit 31，負的 int）：以無號 32 位語意處理，
        // 清除時不得因符號延伸把「高於 32 位」的保留位元一併清掉。
        const int highFlag = unchecked((int)0x8000_0000);
        const long unrelatedHighBit = 1L << 32;                       // 與旗標無關的高位
        long current = unrelatedHighBit | (highFlag & 0xFFFFFFFFL);   // = 0x1_8000_0000

        long cleared = WindowStyleBits.Apply(current, highFlag, enabled: false);

        Assert.Equal(0L, cleared & 0xFFFFFFFFL);   // 低 32 位的目標旗標已清
        Assert.Equal(unrelatedHighBit, cleared);   // 高位保留位元完好，未被符號延伸誤清
    }
}
