namespace TaskBuddyWPF.Services
{
    // Simple cross-page navigation payload: set before calling RootNav.Navigate(...),
    // read and cleared by the destination page once it has selected the matching row.
    // Deliberately minimal — a single pending value is all "Go to details"/"Go to
    // service(s)" needs; no general parameterized-navigation machinery required.
    public static class NavigationTarget
    {
        public static uint? RequestedPid { get; set; }
    }
}
