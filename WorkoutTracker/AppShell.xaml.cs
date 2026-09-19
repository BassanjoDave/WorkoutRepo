using WorkoutTracker.Views;

namespace WorkoutTracker;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Not a tab — reached via GoToAsync with routineId/slot query parameters
		// and pushed over the tab bar, matching the design's full-screen session overlay.
		Routing.RegisterRoute("session", typeof(SessionPage));
		Routing.RegisterRoute("scheduleEditor", typeof(ScheduleEditorPage));
		Routing.RegisterRoute("exerciseDetail", typeof(ExerciseDetailPage));
		Routing.RegisterRoute("manageMembers", typeof(ManageMembersPage));
		Routing.RegisterRoute("memberEdit", typeof(MemberEditPage));
		Routing.RegisterRoute("devSettings", typeof(DevSettingsPage));
		Routing.RegisterRoute("exerciseEditor", typeof(ExerciseEditorPage));
		Routing.RegisterRoute("equipmentPreferences", typeof(EquipmentPreferencesPage));
		Routing.RegisterRoute("myRigs", typeof(MyRigsPage));
		Routing.RegisterRoute("browseRigs", typeof(BrowseRigsPage));
		Routing.RegisterRoute("reminders", typeof(RemindersPage));
		Routing.RegisterRoute("addFoodEntry", typeof(AddFoodEntryPage));
		Routing.RegisterRoute("foodLibrary", typeof(FoodLibraryPage));
		Routing.RegisterRoute("recipeLibrary", typeof(RecipeLibraryPage));
		Routing.RegisterRoute("foodEditor", typeof(FoodEditorPage));
		Routing.RegisterRoute("recipeBuilder", typeof(RecipeBuilderPage));
		Routing.RegisterRoute("macroGoals", typeof(MacroGoalsPage));
		Routing.RegisterRoute("logMeasurement", typeof(LogMeasurementPage));
		Routing.RegisterRoute("customizeHome", typeof(CustomizeHomePage));
		Routing.RegisterRoute("hiitBuilder", typeof(HiitBuilderPage));
		Routing.RegisterRoute("hiitPlayer", typeof(HiitPlayerPage));
		Routing.RegisterRoute("standardBuilder", typeof(StandardBuilderPage));
		Routing.RegisterRoute("stackEditor", typeof(StackEditorPage));
		Routing.RegisterRoute("onboarding", typeof(OnboardingPage));
		Routing.RegisterRoute("upgrade", typeof(UpgradePage));
		Routing.RegisterRoute("progressPhotoViewer", typeof(ProgressPhotoViewerPage));
		Routing.RegisterRoute("progressPhotoGallery", typeof(ProgressPhotoGalleryPage));
		Routing.RegisterRoute("notifications", typeof(NotificationsPage));
		Routing.RegisterRoute("legal", typeof(LegalPage));
		Routing.RegisterRoute("standardAddExercise", typeof(StandardAddExercisePage));
		Routing.RegisterRoute("hiitAddSection", typeof(HiitAddSectionPage));
		Routing.RegisterRoute("browseExercises", typeof(BrowseExercisesPage));
		Routing.RegisterRoute("addFromRitual", typeof(AddFromRitualPage));
		Routing.RegisterRoute("routineSchedule", typeof(RoutineSchedulePage));
		Routing.RegisterRoute("help", typeof(HelpPage));
	}
}
