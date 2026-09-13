using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using WorkoutTracker.Services;
using WorkoutTracker.Services.Storage;
using WorkoutTracker.ViewModels;
using WorkoutTracker.Views;

namespace WorkoutTracker;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		CrashLogger.Install();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
#if ANDROID || IOS
			.UseLocalNotification(config => config.AddCategory(WorkoutReminderService.ReminderCategory))
#else
			.UseLocalNotification()
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Inter-Regular.ttf", "InterRegular");
				fonts.AddFont("Inter-Medium.ttf", "InterMedium");
				fonts.AddFont("Inter-SemiBold.ttf", "InterSemibold");
				fonts.AddFont("Phosphor.ttf", "PhosphorRegular");
				fonts.AddFont("Phosphor-Fill.ttf", "PhosphorFill");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		RegisterServices(builder.Services);
		RegisterViews(builder.Services);

		// IWorkoutReminderService is deliberately NOT eagerly resolved here.
		// On Windows, AppNotificationManager.Register() is intermittently fatal
		// (a native, uncatchable crash — see IWorkoutReminderService's Windows
		// branch) when called this early, before the window and UI message loop
		// exist. Leaving it to construct lazily on first real use (HomeViewModel,
		// after the app is already up and running) avoids that startup race.
		return builder.Build();
	}

	private static void RegisterServices(IServiceCollection services)
	{
		// The repository is the only place a storage backend type may appear.
		// SyncingWorkoutRepository always reads/writes local disk first (so the
		// app still works fully offline) and pushes to the remote API best-effort
		// in the background. Without an API key configured (Developer Settings),
		// pushes just fail silently and stay queued — functionally identical to
		// pure local-only until a key is set, so this is safe to have live now.
		services.AddSingleton<IWorkoutRepository, SyncingWorkoutRepository>();
		services.AddSingleton(new HttpClient());
		services.AddSingleton<RemoteApiWorkoutRepository>();
		services.AddSingleton<IOutboxStore, OutboxStore>();
		services.AddSingleton<ISyncStatusService, SyncStatusService>();
		services.AddSingleton<IActiveSessionService, ActiveSessionService>();
		services.AddSingleton<ISeatAvailabilityService, SeatAvailabilityService>();
		services.AddSingleton<IEntitlementService, EntitlementService>();
		services.AddSingleton<IMemberSplitOffService, MemberSplitOffService>();
		services.AddSingleton<IAppBootstrapper, AppBootstrapper>();
		services.AddSingleton<IHiitSoundService, HiitSoundService>();
		services.AddSingleton<IWorkoutReminderService, WorkoutReminderService>();
		services.AddSingleton<IStackReminderService, StackReminderService>();
		services.AddSingleton<IBiometricAuthService, BiometricAuthService>();
		services.AddSingleton<IProgressPhotoCaptureService, ProgressPhotoCaptureService>();
		services.AddSingleton<IKeyboardService, KeyboardService>();
		services.AddSingleton<IMemberPinService, MemberPinService>();
		services.AddSingleton<IMemberAuthGateService, MemberAuthGateService>();
		services.AddSingleton<IRecipeDraftBridge, RecipeDraftBridge>();
		services.AddSingleton<IHomeWorkoutBridge, HomeWorkoutBridge>();
		services.AddSingleton<IGoogleAuthService, GoogleAuthService>();
		services.AddSingleton<IAppRestartService, AppRestartService>();
		services.AddSingleton<IIdentityService, IdentityService>();
		services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
	}

	private static void RegisterViews(IServiceCollection services)
	{
		services.AddTransient<ProfileGateViewModel>();
		services.AddTransient<ProfileGatePage>();
		services.AddTransient<HomeViewModel>();
		services.AddTransient<HomePage>();
		services.AddTransient<WorkoutsViewModel>();
		services.AddTransient<WorkoutsView>();
		services.AddTransient<SessionViewModel>();
		services.AddTransient<SessionPage>();
		services.AddTransient<LibraryViewModel>();
		services.AddTransient<LibraryView>();
		services.AddTransient<ExerciseViewModel>();
		services.AddTransient<ExercisePage>();
		services.AddTransient<ExerciseDetailViewModel>();
		services.AddTransient<ExerciseDetailPage>();
		services.AddTransient<HistoryViewModel>();
		services.AddTransient<HistoryPage>();
		services.AddTransient<ProfileViewModel>();
		services.AddTransient<ProfilePage>();
		services.AddTransient<ScheduleEditorViewModel>();
		services.AddTransient<ScheduleEditorPage>();
		services.AddTransient<ManageMembersViewModel>();
		services.AddTransient<ManageMembersPage>();
		services.AddTransient<MemberEditViewModel>();
		services.AddTransient<MemberEditPage>();
		services.AddTransient<HiitBuilderViewModel>();
		services.AddTransient<HiitBuilderPage>();
		services.AddTransient<HiitPlayerViewModel>();
		services.AddTransient<HiitPlayerPage>();
		services.AddTransient<StandardBuilderViewModel>();
		services.AddTransient<StandardBuilderPage>();
		services.AddTransient<DevSettingsViewModel>();
		services.AddTransient<DevSettingsPage>();
		services.AddTransient<ExerciseEditorViewModel>();
		services.AddTransient<ExerciseEditorPage>();
		services.AddTransient<EquipmentPreferencesViewModel>();
		services.AddTransient<EquipmentPreferencesPage>();
		services.AddTransient<MyRigsViewModel>();
		services.AddTransient<MyRigsPage>();
		services.AddTransient<BrowseRigsViewModel>();
		services.AddTransient<BrowseRigsPage>();
		services.AddTransient<RemindersViewModel>();
		services.AddTransient<RemindersPage>();
		services.AddTransient<NutritionViewModel>();
		services.AddTransient<NutritionPage>();
		services.AddTransient<AddFoodEntryViewModel>();
		services.AddTransient<AddFoodEntryPage>();
		services.AddTransient<FoodLibraryViewModel>();
		services.AddTransient<FoodLibraryPage>();
		services.AddTransient<RecipeLibraryViewModel>();
		services.AddTransient<RecipeLibraryPage>();
		services.AddTransient<FoodEditorViewModel>();
		services.AddTransient<FoodEditorPage>();
		services.AddTransient<RecipeBuilderViewModel>();
		services.AddTransient<RecipeBuilderPage>();
		services.AddTransient<MacroGoalsViewModel>();
		services.AddTransient<MacroGoalsPage>();
		services.AddTransient<LogMeasurementViewModel>();
		services.AddTransient<LogMeasurementPage>();
		services.AddTransient<MeasurementsViewModel>();
		services.AddTransient<MeasurementsPage>();
		services.AddTransient<ProgressPhotoViewerViewModel>();
		services.AddTransient<ProgressPhotoViewerPage>();
		services.AddTransient<ProgressPhotoGalleryViewModel>();
		services.AddTransient<ProgressPhotoGalleryPage>();
		services.AddTransient<NutritionSummaryViewModel>();
		services.AddTransient<NutritionSummaryView>();
		services.AddTransient<MeasurementsSummaryViewModel>();
		services.AddTransient<MeasurementsSummaryView>();
		services.AddTransient<HistorySummaryViewModel>();
		services.AddTransient<HistorySummaryView>();
		services.AddTransient<CustomizeHomeViewModel>();
		services.AddTransient<CustomizeHomePage>();
		services.AddTransient<StacksViewModel>();
		services.AddTransient<StacksPage>();
		services.AddTransient<StackEditorViewModel>();
		services.AddTransient<StackEditorPage>();
		services.AddTransient<StacksSummaryViewModel>();
		services.AddTransient<StacksSummaryView>();
		services.AddTransient<OnboardingViewModel>();
		services.AddTransient<OnboardingPage>();
		services.AddTransient<NotificationsViewModel>();
		services.AddTransient<NotificationsPage>();
		services.AddTransient<UpgradeViewModel>();
		services.AddTransient<UpgradePage>();
	}
}
