using PoeActTimer.Core;

namespace PoeActTimer.UI;

public sealed record Strings(
    string ActFormat,
    string Running, string Afk, string Menu, string Minimized, string OtherCharacter,
    string ManualPause, string NoRun, string Finished,
    string More, string Less, string LoadingDeducted, string ActDone,
    string HotkeyPause, string HotkeyToggle, string HotkeyReset, string LogMissing,
    string TrayPause, string TrayResume, string TrayStartNow, string TrayReset,
    string TrayHistory, string TrayChooseLog, string TrayExit,
    string ResetConfirm, string StartNowConfirm)
{
    const string Nbsp = " ";

    public static readonly Strings Ru = new(
        ActFormat: "Акт" + Nbsp + "{0}",
        Running: "идёт",
        Afk: "AFK" + Nbsp + "— пауза",
        Menu: "меню" + Nbsp + "— пауза",
        Minimized: "свёрнуто" + Nbsp + "— пауза",
        OtherCharacter: "другой персонаж" + Nbsp + "— пауза",
        ManualPause: "пауза (вручную)",
        NoRun: "нет забега",
        Finished: "забег завершён",
        More: "Подробнее",
        Less: "Кратко",
        LoadingDeducted: "Загрузки (вычтено)",
        ActDone: "завершён",
        HotkeyPause: "Пауза",
        HotkeyToggle: "Подробнее / кратко",
        HotkeyReset: "Сбросить забег",
        LogMissing: "лог не" + Nbsp + "найден",
        TrayPause: "Пауза",
        TrayResume: "Продолжить",
        TrayStartNow: "Начать забег с" + Nbsp + "текущего места",
        TrayReset: "Сбросить забег…",
        TrayHistory: "История забегов",
        TrayChooseLog: "Указать Client.txt…",
        TrayExit: "Выход",
        ResetConfirm: "Сбросить текущий забег? Он уйдёт в" + Nbsp + "историю как незавершённый.",
        StartNowConfirm: "Начать новый забег с" + Nbsp + "текущего акта? Текущий забег уйдёт в" + Nbsp + "историю как незавершённый.");

    public static readonly Strings En = new(
        ActFormat: "Act {0}",
        Running: "running",
        Afk: "AFK — paused",
        Menu: "menu — paused",
        Minimized: "minimized — paused",
        OtherCharacter: "other character — paused",
        ManualPause: "paused (manual)",
        NoRun: "no run",
        Finished: "run finished",
        More: "Details",
        Less: "Compact",
        LoadingDeducted: "Loading (deducted)",
        ActDone: "done",
        HotkeyPause: "Pause",
        HotkeyToggle: "Details / compact",
        HotkeyReset: "Reset run",
        LogMissing: "log not found",
        TrayPause: "Pause",
        TrayResume: "Resume",
        TrayStartNow: "Start run from here",
        TrayReset: "Reset run…",
        TrayHistory: "Run history",
        TrayChooseLog: "Choose Client.txt…",
        TrayExit: "Exit",
        ResetConfirm: "Reset the current run? It will be saved to history as unfinished.",
        StartNowConfirm: "Start a new run from the current act? The current run will be saved to history as unfinished.");

    public static Strings For(string? language) => language == "ru" ? Ru : En;

    public string Act(int number) => string.Format(ActFormat, number);

    public string StatusText(TimerStatus status) => status switch
    {
        TimerStatus.Running => Running,
        TimerStatus.Afk => Afk,
        TimerStatus.Menu => Menu,
        TimerStatus.Minimized => Minimized,
        TimerStatus.OtherCharacter => OtherCharacter,
        TimerStatus.ManualPause => ManualPause,
        TimerStatus.Finished => Finished,
        _ => NoRun,
    };
}
