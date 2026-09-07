using System.Windows;

namespace DesktopPet.UI;

/// <summary>
/// 首次啟動的飼養數量引導視窗（設計檔 §6.5.1）。以 <see cref="Window.ShowDialog"/> 顯示；
/// 使用者點選任一按鈕後把答案存進 <see cref="SelectedPetCount"/>、設定 <c>DialogResult = true</c>
/// 並關閉，呼叫端（E2/E4 的啟動流程）據此決定要建立幾個 <c>Core/PetInstance</c>。
/// </summary>
/// <remarks>
/// 只負責「問使用者選幾隻」這一件事：不建立 <c>Pet</c> 資料、不知道 <c>PetCoordinator</c> 的存在，
/// 避免 UI 層與領域邏輯耦合。「之後可在設置面板調整飼養數量」（§6.5.1 第二句）屬 Phase 2，
/// 設置面板尚未實作。
/// </remarks>
public partial class OnboardingWindow : Window
{
    public OnboardingWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 使用者選擇的飼養數量（1 或 2）。只有在 <see cref="Window.ShowDialog"/> 回傳 <c>true</c>
    /// （使用者實際點了其中一個按鈕，而非直接關閉視窗）時才有意義。
    /// </summary>
    public int SelectedPetCount { get; private set; } = 1;

    private void OnOnePetClick(object sender, RoutedEventArgs e) => Choose(1);

    private void OnTwoPetsClick(object sender, RoutedEventArgs e) => Choose(2);

    private void Choose(int count)
    {
        SelectedPetCount = count;
        DialogResult = true;
        Close();
    }
}
