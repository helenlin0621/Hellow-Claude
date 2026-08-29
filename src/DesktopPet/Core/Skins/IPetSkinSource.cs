using System;
using System.Collections.Generic;
using DesktopPet.Core.Visuals;

namespace DesktopPet.Core.Skins;

/// <summary>
/// 素材來源抽象介面（設計檔 §6.4.4）：把「取得一格畫面」與「查詢某狀態有哪些單元」抽象化，
/// 讓上層渲染邏輯不需知道底層是單格靜態圖還是多格 Sprite Sheet。
/// </summary>
/// <remarks>
/// <b>責任邊界（§7.3.5）：</b>本介面的實作只負責「當前單元播到<b>第幾格</b>」（依
/// <paramref name="elapsed"/> 推算），<b>不決定該播哪個單元</b>——後者是 <c>PetVisualSelector</c>（B5）
/// 的職責。兩者分離，靜態圖與 Sprite Sheet 才不需要兩套邏輯：
/// 靜態實作永遠回傳第 1 格（整張圖），多格實作依時間推進。
/// 具體實作為 <c>StaticImageSkinSource</c> / <c>SpriteSheetSkinSource</c>（B3）。
/// </remarks>
public interface IPetSkinSource
{
    /// <summary>
    /// 取得當前應顯示的一格畫面。
    /// </summary>
    /// <param name="state">當前視覺狀態（決定素材類別）。</param>
    /// <param name="elapsed">進入當前單元後經過的時間，用於決定播到第幾格（靜態圖忽略此參數）。</param>
    FrameRef GetFrame(PetVisualState state, TimeSpan elapsed);

    /// <summary>
    /// 查詢某狀態目前登記的所有動畫單元，供 §6.6.1 面板顯示完成度（例：「4 / 6 類型」）與單元清單用。
    /// 缺素材時回傳空清單。
    /// </summary>
    IReadOnlyList<VisualUnitInfo> GetUnits(PetVisualState state);
}
