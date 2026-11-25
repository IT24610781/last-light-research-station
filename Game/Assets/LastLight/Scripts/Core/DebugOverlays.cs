namespace LastLight.Core
{
    /// <summary>
    /// Runtime switches for the agent decision overlays. They are off during normal play
    /// and toggled with F1-F4 so an agent's reasoning can be shown on demand.
    /// </summary>
    public static class DebugOverlays
    {
        public static bool Hunter;
        public static bool Guard;
        public static bool Repair;
        public static bool Director;

        public static bool Any => Hunter || Guard || Repair || Director;
    }
}
