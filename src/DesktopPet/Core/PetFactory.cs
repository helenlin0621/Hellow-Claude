using DesktopPet.Models;
using Path = System.IO.Path;

namespace DesktopPet.Core;

/// <summary>
/// 新寵物的建立工廠（E2 Onboarding，設計檔 §5.1 / §6.4.1 / §6.5.1）。純邏輯、無 WPF 相依，
/// 可跨平台單元測試。<see cref="Pet"/> 模型刻意不內建遊戲平衡預設值（見其註解），起始數值於此集中指定。
/// </summary>
/// <remarks>
/// <b>起始數值（可調；設計檔未逐項指定，僅要求「建立時指定」）：</b>新寵物一律以「照顧良好」的
/// 中高檔起步（<see cref="StartHunger"/> 低、<see cref="StartEnergy"/> / <see cref="StartHappiness"/> 偏高、
/// <see cref="StartHealth"/> 滿），避免一開養就掉進 <c>SAD</c> / <c>LOW_ENERGY</c>。
/// <para>
/// <b>內建主題（§6.4.1 兩套官方主題）：</b>第 1 隻用 <see cref="BuiltinCatSkinId"/>、第 2 隻用
/// <see cref="BuiltinDogSkinId"/>，讓雙寵物預設外觀不同。圖樣資料夾路徑由 <paramref name="themesBaseDir"/>
/// 與 <c>SkinId</c> 組成（§7.3.3）。
/// </para>
/// </remarks>
public static class PetFactory
{
    /// <summary>內建貓主題資料夾名稱（§6.4.1）。</summary>
    public const string BuiltinCatSkinId = "builtin_cat";

    /// <summary>內建狗主題資料夾名稱（§6.4.1）。</summary>
    public const string BuiltinDogSkinId = "builtin_dog";

    /// <summary>圖樣來源類型標記（§5.1 <see cref="Pet.SkinSourceType"/>）。</summary>
    public const string BuiltinSourceType = "builtin";

    // ── 起始數值（可調；見類別註解）─────────────────────────────
    /// <summary>起始飢餓度（低＝不餓）。</summary>
    public const int StartHunger = 20;
    /// <summary>起始能量（偏高）。</summary>
    public const int StartEnergy = 80;
    /// <summary>起始幸福度（偏高）。</summary>
    public const int StartHappiness = 80;
    /// <summary>起始健康度（滿）。</summary>
    public const int StartHealth = 100;
    /// <summary>起始等級。</summary>
    public const int StartLevel = 1;

    /// <summary>第 1、2 隻寵物依序採用的內建主題（§6.4.1）。</summary>
    private static readonly string[] DefaultSkinRotation = { BuiltinCatSkinId, BuiltinDogSkinId };

    /// <summary>
    /// 建立首次啟動要飼養的 1–2 隻寵物（§6.5.1）。名稱採預設「寵物 1 / 寵物 2」，
    /// 外觀依 <see cref="DefaultSkinRotation"/> 分配（第 1 隻貓、第 2 隻狗）。
    /// </summary>
    /// <param name="count">飼養數量（1 或 2，超出範圍會夾回 1–2）。</param>
    /// <param name="themesBaseDir">內建主題根目錄（如 <c>.../Resources/Assets/Themes</c>）。</param>
    /// <param name="now">建立時刻，寫入 <see cref="Pet.CreatedDate"/> 與 <see cref="Pet.LastTickTime"/>。</param>
    public static List<Pet> CreateInitialPets(int count, string themesBaseDir, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(themesBaseDir);

        count = Math.Clamp(count, 1, DefaultSkinRotation.Length);
        var pets = new List<Pet>(count);
        for (int i = 0; i < count; i++)
        {
            string skinId = DefaultSkinRotation[i];
            pets.Add(Create($"寵物 {i + 1}", skinId, themesBaseDir, now));
        }

        return pets;
    }

    /// <summary>
    /// 建立單一寵物並套用起始數值與內建圖樣。<see cref="Pet.Id"/> 以 GUID 產生，確保雙寵物不重號。
    /// </summary>
    /// <param name="name">寵物名稱。</param>
    /// <param name="skinId">內建主題資料夾名稱（如 <see cref="BuiltinCatSkinId"/>）。</param>
    /// <param name="themesBaseDir">內建主題根目錄。</param>
    /// <param name="now">建立時刻。</param>
    public static Pet Create(string name, string skinId, string themesBaseDir, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(skinId);
        ArgumentNullException.ThrowIfNull(themesBaseDir);

        return new Pet
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            CreatedDate = now,
            Age = 0,
            Hunger = StartHunger,
            Energy = StartEnergy,
            Happiness = StartHappiness,
            Health = StartHealth,
            Level = StartLevel,
            Experience = 0,
            CurrentMood = PetMood.Neutral,
            SkinId = skinId,
            SkinSourceType = BuiltinSourceType,
            SkinFolderPath = Path.Combine(themesBaseDir, skinId),
            // 冷卻時間戳留 default（首次操作必定通過冷卻，見 HappinessManager.TryAward）。
            LastTickTime = now,
        };
    }
}
