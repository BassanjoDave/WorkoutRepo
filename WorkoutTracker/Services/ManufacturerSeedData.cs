// AUTO-GENERATED from design_handoff_workout_tracker/Workout Tracker.dc.html's
// EXERCISES / VIB_EXERCISES / ROUTINE_DEFS arrays — lifted per the handoff's
// instruction to use this data verbatim as manufacturer seed content.
// Regenerate via scratchpad/generate_csharp.py rather than hand-editing.
using WorkoutTracker.Models;
using Visibility = WorkoutTracker.Models.Visibility;

namespace WorkoutTracker.Services;

public static class ManufacturerSeedData
{
    private static readonly DateTimeOffset SeedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // PLACEHOLDER — the handoff data has no per-routine equipment model, only the
    // generic "Bowflex" manufacturer name and an equally generic BowflexMachine
    // equipment tag; there's no real source of truth for which specific model each
    // routine was authored on. Standing in with "Revolution XL" (Dave's own example
    // in the naming-convention spec) so the "[Manufacturer] – [Model]" display has
    // something to show — replace with the real model(s) once known. NOTE: this file
    // is auto-generated from generate_csharp.py; a regeneration will need this
    // Model assignment re-applied (or added to the generator) since it isn't sourced
    // from the handoff data.
    private const string PlaceholderBowflexModel = "Revolution XL";

    public static ManufacturerLibrary Build()
    {
        var library = new ManufacturerLibrary();

        // bench-press
        library.Exercises.Add(new Exercise { Id = Guid.Parse("aa92ec77-c696-502a-93fe-0d5c43237e18"), AccountId = null, OwnerMemberId = null,
            Name = "Bench Press", Category = ExerciseCategory.Chest, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Pectoralis Major, Deltoids, Triceps", Bench = "45° incline", Accessory = "Long Hand Grips", ArmPosition = "7 or 8",
            Tips = new() { "Keep a 90° angle between upper arms and torso.", "Keep shoulder blades pinched together throughout.", "Do not let elbows travel behind your shoulders." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // chest-fly
        library.Exercises.Add(new Exercise { Id = Guid.Parse("ff60403a-382c-54a9-9102-0294949b47da"), AccountId = null, OwnerMemberId = null,
            Name = "Chest Fly", Category = ExerciseCategory.Chest, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Pectoralis Major, Anterior Deltoid", Bench = "45° incline", Accessory = "Long Hand Grips", ArmPosition = "7 or 8",
            Tips = new() { "Maintain a 60–90° angle between upper arms and torso.", "Keep knees bent, feet on floor, head back against bench.", "Press straight out and slowly return to start." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // standing-chest-press
        library.Exercises.Add(new Exercise { Id = Guid.Parse("c154b8b6-81dd-5531-8faf-7f5bd4bc6ca8"), AccountId = null, OwnerMemberId = null,
            Name = "Standing Chest Press", Category = ExerciseCategory.Chest, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Pectoralis Major, Deltoids, Triceps", Bench = "Standing, facing engine", Accessory = "Long Hand Grips", ArmPosition = "6",
            Tips = new() { "Stagger your stance for balance.", "Keep elbows slightly below shoulder height.", "Press straight forward, control the return." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // decline-chest-fly
        library.Exercises.Add(new Exercise { Id = Guid.Parse("e1e62ccb-3112-55bc-bdb2-0846266e6e8c"), AccountId = null, OwnerMemberId = null,
            Name = "Decline Chest Fly", Category = ExerciseCategory.Chest, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Lower Pectoralis Major", Bench = "Decline (45°, reversed)", Accessory = "Long Hand Grips", ArmPosition = "2 or 3",
            Tips = new() { "Keep a slight bend in the elbows throughout.", "Squeeze chest at full contraction.", "Lower slowly under control." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // seated-shoulder-press
        library.Exercises.Add(new Exercise { Id = Guid.Parse("05cd28e4-7534-58b6-b2e0-7c895a81f516"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Shoulder Press", Category = ExerciseCategory.Shoulders, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Deltoids, Triceps", Bench = "Upright, seated", Accessory = "Long Hand Grips", ArmPosition = "8 or 9",
            Tips = new() { "Keep wrists stacked over elbows.", "Avoid arching your lower back.", "Press straight overhead, don’t lock out hard." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // rear-deltoid-row
        library.Exercises.Add(new Exercise { Id = Guid.Parse("7a9fc827-f764-59ba-b94b-a11e12f03d6b"), AccountId = null, OwnerMemberId = null,
            Name = "Rear Deltoid Row", Category = ExerciseCategory.Shoulders, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Posterior Deltoid, Rhomboids", Bench = "Upright, facing engine", Accessory = "Long Hand Grips", ArmPosition = "6",
            Tips = new() { "Lead with your elbows, not your hands.", "Squeeze shoulder blades together at the back.", "Keep torso still throughout the row." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // standing-lateral-raise
        library.Exercises.Add(new Exercise { Id = Guid.Parse("8be4bedf-130e-575d-9599-4dd9e99dd14d"), AccountId = null, OwnerMemberId = null,
            Name = "Standing Lateral Raise", Category = ExerciseCategory.Shoulders, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Medial Deltoid", Bench = "Standing", Accessory = "Long Hand Grips", ArmPosition = "2",
            Tips = new() { "Raise arms to shoulder height, no higher.", "Keep a slight bend in the elbows.", "Lower slowly, resisting the whole way." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // shoulder-shrug
        library.Exercises.Add(new Exercise { Id = Guid.Parse("031e3788-209c-5ffd-bd40-b60f6ad40d2d"), AccountId = null, OwnerMemberId = null,
            Name = "Shoulder Shrug", Category = ExerciseCategory.Shoulders, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Trapezius", Bench = "Standing", Accessory = "Long Hand Grips", ArmPosition = "1",
            Tips = new() { "Lift straight up, don’t roll the shoulders.", "Pause briefly at the top.", "Keep arms relaxed and straight." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // seated-lat-row
        library.Exercises.Add(new Exercise { Id = Guid.Parse("3eb86938-0037-51d4-9421-dac0d988605e"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Lat Row", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Latissimus Dorsi, Biceps", Bench = "Seated, upright", Accessory = "Long Hand Grips", ArmPosition = "4",
            Tips = new() { "Pull elbows straight back, close to the body.", "Keep chest lifted, don’t round the back.", "Squeeze shoulder blades at the finish." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // standing-lat-row
        library.Exercises.Add(new Exercise { Id = Guid.Parse("f0484f54-a3c0-5ade-a443-9941b074d0a8"), AccountId = null, OwnerMemberId = null,
            Name = "Standing Lat Row", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Latissimus Dorsi, Biceps", Bench = "Standing, facing engine", Accessory = "Long Hand Grips", ArmPosition = "2",
            Tips = new() { "Hinge slightly at the hips, keep spine neutral.", "Drive elbows back and down.", "Control the return, don’t let it snap forward." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // narrow-pulldown
        library.Exercises.Add(new Exercise { Id = Guid.Parse("7499b045-5fc2-51a8-9c35-8ab24aafdb30"), AccountId = null, OwnerMemberId = null,
            Name = "Narrow Pulldown", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Latissimus Dorsi, Biceps", Bench = "Seated", Accessory = "Curl/Pulldown Bar", ArmPosition = "9",
            Tips = new() { "Pull the bar down to upper chest level.", "Keep elbows close to your sides.", "Avoid leaning back excessively." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // stiff-arm-pulldown
        library.Exercises.Add(new Exercise { Id = Guid.Parse("8217b763-f226-5e1f-ab66-7036adc96d3e"), AccountId = null, OwnerMemberId = null,
            Name = "Stiff Arm Pulldown", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Latissimus Dorsi", Bench = "Standing", Accessory = "Curl/Pulldown Bar", ArmPosition = "9",
            Tips = new() { "Keep elbows nearly straight throughout.", "Pull down using your lats, not your arms.", "Hinge slightly forward at the hips." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // standing-biceps-curl
        library.Exercises.Add(new Exercise { Id = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), AccountId = null, OwnerMemberId = null,
            Name = "Standing Biceps Curl", Category = ExerciseCategory.Arms, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Biceps Brachii", Bench = "Standing", Accessory = "Long Hand Grips", ArmPosition = "1",
            Tips = new() { "Keep elbows pinned to your sides.", "Curl using a full range of motion.", "Don’t swing your torso to assist." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // triceps-pushdown
        library.Exercises.Add(new Exercise { Id = Guid.Parse("74a41b00-8711-5a59-864d-70e02eb6e730"), AccountId = null, OwnerMemberId = null,
            Name = "Triceps Pushdown", Category = ExerciseCategory.Arms, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Triceps Brachii", Bench = "Standing", Accessory = "Curl/Pulldown Bar", ArmPosition = "9",
            Tips = new() { "Keep elbows tucked at your sides.", "Extend fully without locking hard.", "Control the return to the start position." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // french-press
        library.Exercises.Add(new Exercise { Id = Guid.Parse("9d59278b-5e9f-583a-bb81-8e6db061dda3"), AccountId = null, OwnerMemberId = null,
            Name = "French Press", Category = ExerciseCategory.Arms, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Triceps Brachii (long head)", Bench = "Seated or standing", Accessory = "Long Hand Grips", ArmPosition = "9",
            Tips = new() { "Keep upper arms stationary and vertical.", "Extend overhead through full range.", "Avoid flaring the elbows outward." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // seated-triceps-extension
        library.Exercises.Add(new Exercise { Id = Guid.Parse("dfd30ba7-6430-5bc8-aec7-78d426abdab4"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Triceps Extension", Category = ExerciseCategory.Arms, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Triceps Brachii", Bench = "Seated, upright", Accessory = "Long Hand Grips", ArmPosition = "8",
            Tips = new() { "Keep elbows close to your head.", "Extend arms fully, then control the return.", "Keep torso upright throughout." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // seated-crunch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("02b98b0d-7e10-542b-be14-a3e646ba1311"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Resisted Abdominal Crunch", Category = ExerciseCategory.Abs, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Rectus Abdominis", Bench = "Seated", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Curl your ribcage toward your pelvis.", "Don’t lift your head or chin to help.", "Use slow, controlled motion — no kicking." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // seated-oblique-crunch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("63c78d6b-fd44-5650-95a2-a029e242c9ec"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Resisted Oblique Crunch", Category = ExerciseCategory.Abs, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Obliques", Bench = "Seated", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Rotate through the torso, not the arms.", "Keep hips facing forward.", "Control the return each rep." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // trunk-rotation
        library.Exercises.Add(new Exercise { Id = Guid.Parse("291c7947-a8f4-5579-8f39-4bc532dd96eb"), AccountId = null, OwnerMemberId = null,
            Name = "Standing Trunk Rotation", Category = ExerciseCategory.Abs, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Obliques, Core", Bench = "Standing", Accessory = "Long Hand Grips", ArmPosition = "6",
            Tips = new() { "Keep hips relatively stable as you rotate.", "Rotate through your torso, not your arms.", "Move slowly and with control both ways." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // low-back-extension
        library.Exercises.Add(new Exercise { Id = Guid.Parse("f3b5997b-8474-5e3c-b7f9-f9c02a006e0b"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Low Back Extension", Category = ExerciseCategory.Abs, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Erector Spinae", Bench = "Seated", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Extend through the lower back, not the neck.", "Keep the motion slow and controlled.", "Don’t hyperextend at the top." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // leg-extension
        library.Exercises.Add(new Exercise { Id = Guid.Parse("18a09b84-0ec0-5491-ad50-dd981fc67194"), AccountId = null, OwnerMemberId = null,
            Name = "Leg Extension", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Quadriceps", Bench = "Seated, Leg Extension attachment", Accessory = "Leg Extension pad", ArmPosition = "—",
            Tips = new() { "Extend legs fully without locking knees hard.", "Keep your back against the bench.", "Lower slowly under control." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // leg-curl
        library.Exercises.Add(new Exercise { Id = Guid.Parse("936eb155-cf1b-5f64-af3f-fb5791f02cd2"), AccountId = null, OwnerMemberId = null,
            Name = "Leg Curl", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Hamstrings", Bench = "Seated or lying, Leg Curl attachment", Accessory = "Leg Curl pad", ArmPosition = "—",
            Tips = new() { "Curl heels toward your glutes.", "Keep hips pressed down throughout.", "Control the return each rep." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // leg-press
        library.Exercises.Add(new Exercise { Id = Guid.Parse("f39d2682-2a58-593e-804e-d4bc788d2e54"), AccountId = null, OwnerMemberId = null,
            Name = "Leg Press", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Quadriceps, Glutes, Hamstrings", Bench = "Seated, Leg Press plate", Accessory = "Leg Press plate", ArmPosition = "—",
            Tips = new() { "Keep knees tracking over your toes.", "Don’t lock knees out at extension.", "Press through your whole foot." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // seated-calf-raise
        library.Exercises.Add(new Exercise { Id = Guid.Parse("0a91996b-45a6-5c81-873b-49fcc537f3ca"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Calf Raise", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Gastrocnemius, Soleus", Bench = "Seated, Leg Press plate", Accessory = "Leg Press plate", ArmPosition = "—",
            Tips = new() { "Rise onto the balls of your feet fully.", "Pause briefly at the top.", "Lower slowly for a full stretch." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // jog
        library.Exercises.Add(new Exercise { Id = Guid.Parse("8172e113-41ff-56f9-8f37-421795654aab"), AccountId = null, OwnerMemberId = null,
            Name = "Jog", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Cardiovascular System, Legs", Bench = "Outdoors or treadmill", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Warm up with an easy walk for 3-5 minutes first.", "Keep a conversational pace you can sustain.", "Land softly and keep your posture upright." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // walk
        library.Exercises.Add(new Exercise { Id = Guid.Parse("dc190a4c-69be-52f0-9de2-4de5942dbcfe"), AccountId = null, OwnerMemberId = null,
            Name = "Walk", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Cardiovascular System, Legs", Bench = "Outdoors or treadmill", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Keep a brisk, steady pace.", "Swing your arms naturally to engage more muscle.", "Stand tall — avoid hunching over." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // stationary-bike
        library.Exercises.Add(new Exercise { Id = Guid.Parse("3ed3980e-651b-5481-8673-215f72a2ca0e"), AccountId = null, OwnerMemberId = null,
            Name = "Stationary Bike", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.CardioMachine,
            Muscles = "Cardiovascular System, Quadriceps, Hamstrings", Bench = "Seated on bike", Accessory = "Bike console", ArmPosition = "—",
            Tips = new() { "Adjust seat height so knees stay slightly bent at full extension.", "Keep a steady cadence throughout.", "Increase resistance gradually as you build endurance." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // treadmill
        library.Exercises.Add(new Exercise { Id = Guid.Parse("9191dbdd-d4b2-5bf3-b456-e24c823c63fb"), AccountId = null, OwnerMemberId = null,
            Name = "Treadmill", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.CardioMachine,
            Muscles = "Cardiovascular System, Legs", Bench = "Standing on belt", Accessory = "Treadmill console", ArmPosition = "—",
            Tips = new() { "Start at a walking pace to warm up.", "Keep your eyes forward, not down at your feet.", "Use the incline sparingly at first." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // rowing
        library.Exercises.Add(new Exercise { Id = Guid.Parse("9611c4cf-e552-5b83-93fc-e8c6606de7d8"), AccountId = null, OwnerMemberId = null,
            Name = "Rowing", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.BowflexMachine,
            Muscles = "Full Body, Back, Legs", Bench = "Aerobic Rowing position", Accessory = "Long Hand Grips", ArmPosition = "6",
            Tips = new() { "Drive with your legs first, then lean back, then pull your arms in.", "Keep a smooth, continuous motion.", "Maintain steady breathing throughout the interval." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // elliptical
        library.Exercises.Add(new Exercise { Id = Guid.Parse("d8fad19c-3cc5-5aca-9d2d-a12d8214b472"), AccountId = null, OwnerMemberId = null,
            Name = "Elliptical", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.CardioMachine,
            Muscles = "Cardiovascular System, Full Body", Bench = "Standing on pedals", Accessory = "Elliptical console", ArmPosition = "—",
            Tips = new() { "Keep your posture upright, don’t lean on the handles.", "Push and pull evenly through the full stride.", "Alternate direction occasionally to vary muscle use." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // stair-climber
        library.Exercises.Add(new Exercise { Id = Guid.Parse("f720577a-2347-59b1-8eca-274cc58ba637"), AccountId = null, OwnerMemberId = null,
            Name = "Stair Climber", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.CardioMachine,
            Muscles = "Glutes, Quadriceps, Calves", Bench = "Standing on steps", Accessory = "Stair climber console", ArmPosition = "—",
            Tips = new() { "Avoid leaning heavily on the handrails.", "Take full steps rather than tiny ones.", "Keep your core engaged for stability." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // custom-cardio
        library.Exercises.Add(new Exercise { Id = Guid.Parse("680a5b88-93df-595c-84bc-1752aff51e7e"), AccountId = null, OwnerMemberId = null,
            Name = "Custom Cardio", Category = ExerciseCategory.Cardio, Equipment = ExerciseEquipment.Custom,
            Muscles = "Varies by activity", Bench = "—", Accessory = "—", ArmPosition = "—",
            Tips = new() { "Log any cardio activity not listed here.", "Track resistance, time and distance as available.", "Keep intensity appropriate to your fitness level." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = true, UpdatedAt = SeedTime });

        // hamstring-stretch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("d58511dc-9272-5c1d-91d8-5c5eaf55d22b"), AccountId = null, OwnerMemberId = null,
            Name = "Hamstring Stretch", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.Stretch,
            Muscles = "Hamstrings", Bench = "Floor or standing", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Hinge from the hips, keeping your back flat.", "Ease into the stretch — never bounce.", "Hold for 20-30 seconds per side." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // quad-stretch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("c6c889e7-489d-5d9c-8ec8-3b348666c1c9"), AccountId = null, OwnerMemberId = null,
            Name = "Quad Stretch", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.Stretch,
            Muscles = "Quadriceps", Bench = "Standing", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Keep knees close together as you pull your heel back.", "Stand tall — don’t arch your lower back.", "Hold for 20-30 seconds per side." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // shoulder-stretch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("785478de-adc7-52da-ac6e-5244e91ca134"), AccountId = null, OwnerMemberId = null,
            Name = "Shoulder Stretch", Category = ExerciseCategory.Shoulders, Equipment = ExerciseEquipment.Stretch,
            Muscles = "Deltoids, Rotator Cuff", Bench = "Standing", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Pull the arm gently across your chest.", "Keep the shoulder relaxed, away from your ear.", "Hold for 20-30 seconds per side." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // cat-cow-stretch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("0a2150ce-2ab4-5784-8157-f2b56531f898"), AccountId = null, OwnerMemberId = null,
            Name = "Cat-Cow Stretch", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.Stretch,
            Muscles = "Spinal Erectors, Core", Bench = "Floor, on hands and knees", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Move slowly between arching and rounding your back.", "Sync the movement with your breath.", "Keep wrists stacked under shoulders." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // childs-pose
        library.Exercises.Add(new Exercise { Id = Guid.Parse("59cce029-7803-5ee7-86d0-71880f27fe48"), AccountId = null, OwnerMemberId = null,
            Name = "Child’s Pose", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.Stretch,
            Muscles = "Lats, Spinal Erectors", Bench = "Floor", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Sit back toward your heels, arms extended forward.", "Relax your shoulders down and away from your ears.", "Breathe deeply and hold for 30 seconds or more." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // push-up
        library.Exercises.Add(new Exercise { Id = Guid.Parse("0f01ddb1-d5da-5f10-bcbc-a4aef3488d18"), AccountId = null, OwnerMemberId = null,
            Name = "Push-Up", Category = ExerciseCategory.Chest, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Pectoralis Major, Triceps, Deltoids", Bench = "Floor", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Keep your body in a straight line from head to heels.", "Lower until your chest nearly touches the floor.", "Keep elbows at roughly 45° from your torso." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // bodyweight-squat
        library.Exercises.Add(new Exercise { Id = Guid.Parse("cf736efd-45ad-5734-ab65-19e9b0b8136a"), AccountId = null, OwnerMemberId = null,
            Name = "Bodyweight Squat", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Quadriceps, Glutes, Hamstrings", Bench = "Floor", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Keep your chest up and knees tracking over your toes.", "Sit back like you’re lowering onto a chair.", "Drive through your heels to stand." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // plank
        library.Exercises.Add(new Exercise { Id = Guid.Parse("2c06b00a-2d09-599b-ac48-c739f354b296"), AccountId = null, OwnerMemberId = null,
            Name = "Plank", Category = ExerciseCategory.Abs, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Rectus Abdominis, Core", Bench = "Floor", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Keep a straight line from head to heels.", "Squeeze your glutes and brace your core.", "Don’t let your hips sag or pike up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // walking-lunge
        library.Exercises.Add(new Exercise { Id = Guid.Parse("eb87967d-2916-5de0-aedc-3ff0006bab0e"), AccountId = null, OwnerMemberId = null,
            Name = "Walking Lunge", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Quadriceps, Glutes, Hamstrings", Bench = "Floor", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Step forward into a controlled lunge, knee over ankle.", "Keep your torso upright throughout.", "Push off the front foot to step into the next rep." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // burpee
        library.Exercises.Add(new Exercise { Id = Guid.Parse("3a3216ee-1080-573b-8bdc-e5b6abec54ae"), AccountId = null, OwnerMemberId = null,
            Name = "Burpee", Category = ExerciseCategory.FullBody, Equipment = ExerciseEquipment.Bodyweight,
            Muscles = "Full Body", Bench = "Floor", Accessory = "None", ArmPosition = "—",
            Tips = new() { "Move with control on the way down to protect your back.", "Land softly on the jump.", "Keep a steady pace you can sustain for reps." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // kettlebell-swing
        library.Exercises.Add(new Exercise { Id = Guid.Parse("b18ff3e4-7873-5434-bf5c-fd2c585501a9"), AccountId = null, OwnerMemberId = null,
            Name = "Kettlebell Swing", Category = ExerciseCategory.FullBody, Equipment = ExerciseEquipment.Kettlebell,
            Muscles = "Glutes, Hamstrings, Core", Bench = "Standing", Accessory = "Kettlebell", ArmPosition = "—",
            Tips = new() { "Hinge at the hips, don’t squat the swing.", "Drive with your hips to snap the kettlebell up.", "Keep your back flat and core braced throughout." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // kettlebell-goblet-squat
        library.Exercises.Add(new Exercise { Id = Guid.Parse("ad45b644-e438-5d07-8fbf-3d5bb3c8225f"), AccountId = null, OwnerMemberId = null,
            Name = "Kettlebell Goblet Squat", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.Kettlebell,
            Muscles = "Quadriceps, Glutes", Bench = "Standing", Accessory = "Kettlebell", ArmPosition = "—",
            Tips = new() { "Hold the kettlebell close to your chest.", "Keep your elbows inside your knees at the bottom.", "Drive through your heels to stand." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // kettlebell-deadlift
        library.Exercises.Add(new Exercise { Id = Guid.Parse("df8af844-3a06-5573-94e3-eafb87633a73"), AccountId = null, OwnerMemberId = null,
            Name = "Kettlebell Deadlift", Category = ExerciseCategory.Legs, Equipment = ExerciseEquipment.Kettlebell,
            Muscles = "Hamstrings, Glutes, Lower Back", Bench = "Standing", Accessory = "Kettlebell", ArmPosition = "—",
            Tips = new() { "Keep the kettlebell close to your shins.", "Hinge at the hips with a flat back.", "Drive your hips forward to stand tall." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // turkish-get-up
        library.Exercises.Add(new Exercise { Id = Guid.Parse("a3a8d668-c943-5541-8ed9-5837805c6463"), AccountId = null, OwnerMemberId = null,
            Name = "Turkish Get-Up", Category = ExerciseCategory.FullBody, Equipment = ExerciseEquipment.Kettlebell,
            Muscles = "Full Body, Shoulders, Core", Bench = "Floor", Accessory = "Kettlebell", ArmPosition = "—",
            Tips = new() { "Keep your eyes on the kettlebell throughout the movement.", "Move slowly and with control through each step.", "Keep the arm locked out straight overhead." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // kettlebell-row
        library.Exercises.Add(new Exercise { Id = Guid.Parse("381e87f2-363d-569f-854c-10ee2bf16672"), AccountId = null, OwnerMemberId = null,
            Name = "Kettlebell Row", Category = ExerciseCategory.Back, Equipment = ExerciseEquipment.Kettlebell,
            Muscles = "Latissimus Dorsi, Rhomboids, Biceps", Bench = "Bent over, standing", Accessory = "Kettlebell", ArmPosition = "—",
            Tips = new() { "Keep your back flat, hinge from the hips.", "Pull the kettlebell to your hip, elbow close to your body.", "Control the descent each rep." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = false, IsCardio = false, UpdatedAt = SeedTime });

        // vib-static-squat
        library.Exercises.Add(new Exercise { Id = Guid.Parse("b353b571-0ead-542f-b74d-7aecc0e9b480"), AccountId = null, OwnerMemberId = null,
            Name = "Static Squat Hold", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Quadriceps, Glutes", Bench = "On plate — Shoulder-width", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Hold a 90° knee bend with your chest up.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-deep-squat
        library.Exercises.Add(new Exercise { Id = Guid.Parse("7a78ea8f-894d-5cbc-8912-771863643a77"), AccountId = null, OwnerMemberId = null,
            Name = "Deep Squat Hold", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Glutes, Quadriceps, Adductors", Bench = "On plate — Wide stance", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Sit as low as your mobility allows, heels down.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-dynamic-squat
        library.Exercises.Add(new Exercise { Id = Guid.Parse("5d526d07-6bfc-5af4-b3e6-9e3c8fd4f8b2"), AccountId = null, OwnerMemberId = null,
            Name = "Dynamic Squat", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Quadriceps, Glutes", Bench = "On plate — Shoulder-width", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Move slowly — 3 seconds down, 3 seconds up.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-single-leg-squat
        library.Exercises.Add(new Exercise { Id = Guid.Parse("ec92ad62-fff4-59e3-b712-b2db15869f2c"), AccountId = null, OwnerMemberId = null,
            Name = "Single-Leg Squat", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Quadriceps, Glutes, Stabilizers", Bench = "On plate — Single leg", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Keep the free leg off the plate and hips level.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-lunge
        library.Exercises.Add(new Exercise { Id = Guid.Parse("47bbfbac-917c-561d-a8b2-c4996b21aa0e"), AccountId = null, OwnerMemberId = null,
            Name = "Split Lunge", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Quadriceps, Glutes, Hip Flexors", Bench = "On plate — Staggered / split", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Front foot on the plate, back knee soft.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-calf-raise
        library.Exercises.Add(new Exercise { Id = Guid.Parse("54a818f2-0341-5d45-ba45-fd914bdfffd8"), AccountId = null, OwnerMemberId = null,
            Name = "Calf Raise", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Gastrocnemius, Soleus", Bench = "On plate — Balls of feet / toes", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Rise onto the balls of your feet and hold.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-standing-posture
        library.Exercises.Add(new Exercise { Id = Guid.Parse("f8d90bcf-8dec-5da2-be85-86103fe48a05"), AccountId = null, OwnerMemberId = null,
            Name = "Standing Whole-Body", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Postural Chain, Core", Bench = "On plate — Shoulder-width", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Knees slightly bent — never lock them out on a plate.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-push-up
        library.Exercises.Add(new Exercise { Id = Guid.Parse("8c155fe7-8d27-58c1-a80a-de5839073df0"), AccountId = null, OwnerMemberId = null,
            Name = "Push-Up Hold", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Pectorals, Triceps, Core", Bench = "On plate — Hands on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Hands on the plate, body in one straight line.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-plank
        library.Exercises.Add(new Exercise { Id = Guid.Parse("51cb5893-21e8-5129-a371-8d5c95f90557"), AccountId = null, OwnerMemberId = null,
            Name = "Forearm Plank", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Rectus Abdominis, Core", Bench = "On plate — Forearms on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Brace hard — the vibration will try to break your line.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-side-plank
        library.Exercises.Add(new Exercise { Id = Guid.Parse("8b27338d-34cc-5036-be4a-2e8f4d93bb1a"), AccountId = null, OwnerMemberId = null,
            Name = "Side Plank", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Obliques, Core", Bench = "On plate — Forearms on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "One forearm on the plate, hips stacked and lifted.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-glute-bridge
        library.Exercises.Add(new Exercise { Id = Guid.Parse("ec47922b-5cc7-5d13-85e5-de5a907d425e"), AccountId = null, OwnerMemberId = null,
            Name = "Glute Bridge", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Glutes, Hamstrings", Bench = "On plate — Lying, feet on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Drive the hips up and squeeze at the top.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-crunch
        library.Exercises.Add(new Exercise { Id = Guid.Parse("c5a72cbf-0279-50e4-b142-5fe3d9440933"), AccountId = null, OwnerMemberId = null,
            Name = "Crunch", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Rectus Abdominis", Bench = "On plate — Lying, feet on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Heels on the plate, curl the ribs toward the hips.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-superman
        library.Exercises.Add(new Exercise { Id = Guid.Parse("dc9d6391-c596-5f4c-b899-c1d28f7de92c"), AccountId = null, OwnerMemberId = null,
            Name = "Prone Superman Hold", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Erector Spinae, Glutes", Bench = "On plate — Forearms on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Lift chest and thighs slightly — no neck strain.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-triceps-dip
        library.Exercises.Add(new Exercise { Id = Guid.Parse("eb028230-ba5a-5974-a9f3-8e54db283459"), AccountId = null, OwnerMemberId = null,
            Name = "Triceps Dip", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Triceps", Bench = "On plate — Hands on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Hands behind you on the plate, elbows tracking back.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-strap-curl
        library.Exercises.Add(new Exercise { Id = Guid.Parse("99059b2c-4a3f-5146-8a6c-f9c49e10034f"), AccountId = null, OwnerMemberId = null,
            Name = "Strap Biceps Curl", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Biceps Brachii", Bench = "On plate — Shoulder-width", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Stand on the plate holding the straps, elbows pinned.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-strap-row
        library.Exercises.Add(new Exercise { Id = Guid.Parse("479c046b-d2b5-5099-90b3-b9fc4bb1200f"), AccountId = null, OwnerMemberId = null,
            Name = "Strap Row", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Lats, Rhomboids", Bench = "On plate — Shoulder-width", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Hinge slightly and pull the straps to your hips.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-calf-massage
        library.Exercises.Add(new Exercise { Id = Guid.Parse("363bac5e-073b-51f8-9f68-b4907c7c1fbd"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Calf Massage", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Calves (recovery)", Bench = "On plate — Seated, feet on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Low frequency — this is recovery, not training.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-hamstring-massage
        library.Exercises.Add(new Exercise { Id = Guid.Parse("ced784cd-8aa4-5bd3-ad93-029b836214c8"), AccountId = null, OwnerMemberId = null,
            Name = "Seated Hamstring Massage", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Hamstrings (recovery)", Bench = "On plate — Seated on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Sit directly on the plate and let the muscle loosen.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // vib-kneeling-shoulder
        library.Exercises.Add(new Exercise { Id = Guid.Parse("a6c586d9-e176-509b-82d4-faf3ec89d339"), AccountId = null, OwnerMemberId = null,
            Name = "Kneeling Shoulder Press-Up", Category = ExerciseCategory.VibrationPlate, Equipment = ExerciseEquipment.VibrationPlate,
            Muscles = "Deltoids, Core", Bench = "On plate — Kneeling on plate", Accessory = "Vibration plate", ArmPosition = "—",
            Tips = new() { "Kneel on the plate, hands down, shoulders packed.", "Keep knees soft — never lock joints on a vibrating platform.", "Start with short holds at low frequency and build up." }, Visibility = Visibility.Manufacturer,
            IsVibrationPlate = true, IsCardio = false, UpdatedAt = SeedTime });

        // better-body: 20 Minute Better Body
        library.Routines.Add(new RoutineDefinition { Id = Guid.Parse("5e3d0f03-f861-5d0e-bb79-583e74d5fd0b"), AccountId = Guid.Empty, OwnerMemberId = null,
            OwnerNameSnapshot = "Bowflex", Model = PlaceholderBowflexModel, Name = "20 Minute Better Body", Visibility = Visibility.Manufacturer, Type = RoutineType.Standard,
            UpdatedAt = SeedTime, Exercises = new List<RoutineExerciseTarget> {
                new() { ExerciseId = Guid.Parse("3eb86938-0037-51d4-9421-dac0d988605e"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("7a9fc827-f764-59ba-b94b-a11e12f03d6b"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("dfd30ba7-6430-5bc8-aec7-78d426abdab4"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("f3b5997b-8474-5e3c-b7f9-f9c02a006e0b"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("02b98b0d-7e10-542b-be14-a3e646ba1311"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("aa92ec77-c696-502a-93fe-0d5c43237e18"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("18a09b84-0ec0-5491-ad50-dd981fc67194"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
                new() { ExerciseId = Guid.Parse("936eb155-cf1b-5f64-af3f-fb5791f02cd2"), Groups = { new SetGroupTarget { Sets = "2", Reps = "10-15" } } },
            } });

        // adv-conditioning: Advanced General Conditioning
        library.Routines.Add(new RoutineDefinition { Id = Guid.Parse("19a3eaf0-73b6-563e-9f6b-9d26572ce252"), AccountId = Guid.Empty, OwnerMemberId = null,
            OwnerNameSnapshot = "Bowflex", Model = PlaceholderBowflexModel, Name = "Advanced General Conditioning", Visibility = Visibility.Manufacturer, Type = RoutineType.Standard,
            UpdatedAt = SeedTime, Exercises = new List<RoutineExerciseTarget> {
                new() { ExerciseId = Guid.Parse("c154b8b6-81dd-5531-8faf-7f5bd4bc6ca8"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("05cd28e4-7534-58b6-b2e0-7c895a81f516"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("74a41b00-8711-5a59-864d-70e02eb6e730"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("9d59278b-5e9f-583a-bb81-8e6db061dda3"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("f39d2682-2a58-593e-804e-d4bc788d2e54"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("0a91996b-45a6-5c81-873b-49fcc537f3ca"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("f0484f54-a3c0-5ade-a443-9941b074d0a8"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("8217b763-f226-5e1f-ab66-7036adc96d3e"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("f3b5997b-8474-5e3c-b7f9-f9c02a006e0b"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("02b98b0d-7e10-542b-be14-a3e646ba1311"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
            } });

        // upper-lower: 20 Minute Upper/Lower Body
        library.Routines.Add(new RoutineDefinition { Id = Guid.Parse("0f10d7c0-e148-5b8e-a71a-0ea94875c047"), AccountId = Guid.Empty, OwnerMemberId = null,
            OwnerNameSnapshot = "Bowflex", Model = PlaceholderBowflexModel, Name = "20 Minute Upper/Lower Body", Visibility = Visibility.Manufacturer, Type = RoutineType.Standard,
            UpdatedAt = SeedTime, Exercises = new List<RoutineExerciseTarget> {
                new() { ExerciseId = Guid.Parse("aa92ec77-c696-502a-93fe-0d5c43237e18"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("3eb86938-0037-51d4-9421-dac0d988605e"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("05cd28e4-7534-58b6-b2e0-7c895a81f516"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("9d59278b-5e9f-583a-bb81-8e6db061dda3"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("18a09b84-0ec0-5491-ad50-dd981fc67194"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("936eb155-cf1b-5f64-af3f-fb5791f02cd2"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("f39d2682-2a58-593e-804e-d4bc788d2e54"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("0a91996b-45a6-5c81-873b-49fcc537f3ca"), Groups = { new SetGroupTarget { Sets = "3", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("f3b5997b-8474-5e3c-b7f9-f9c02a006e0b"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
                new() { ExerciseId = Guid.Parse("02b98b0d-7e10-542b-be14-a3e646ba1311"), Groups = { new SetGroupTarget { Sets = "3", Reps = "10-12" } } },
            } });

        // body-building: Body Building
        library.Routines.Add(new RoutineDefinition { Id = Guid.Parse("2578a9e7-ff7b-5790-915f-99c27777b8b7"), AccountId = Guid.Empty, OwnerMemberId = null,
            OwnerNameSnapshot = "Bowflex", Model = PlaceholderBowflexModel, Name = "Body Building", Visibility = Visibility.Manufacturer, Type = RoutineType.Standard,
            UpdatedAt = SeedTime, Exercises = new List<RoutineExerciseTarget> {
                new() { ExerciseId = Guid.Parse("aa92ec77-c696-502a-93fe-0d5c43237e18"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("e1e62ccb-3112-55bc-bdb2-0846266e6e8c"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("05cd28e4-7534-58b6-b2e0-7c895a81f516"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("7a9fc827-f764-59ba-b94b-a11e12f03d6b"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("8be4bedf-130e-575d-9599-4dd9e99dd14d"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("031e3788-209c-5ffd-bd40-b60f6ad40d2d"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("3eb86938-0037-51d4-9421-dac0d988605e"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("7499b045-5fc2-51a8-9c35-8ab24aafdb30"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("dfd30ba7-6430-5bc8-aec7-78d426abdab4"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("9d59278b-5e9f-583a-bb81-8e6db061dda3"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("18a09b84-0ec0-5491-ad50-dd981fc67194"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("936eb155-cf1b-5f64-af3f-fb5791f02cd2"), Groups = { new SetGroupTarget { Sets = "3", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("f39d2682-2a58-593e-804e-d4bc788d2e54"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("0a91996b-45a6-5c81-873b-49fcc537f3ca"), Groups = { new SetGroupTarget { Sets = "4", Reps = "12-15" } } },
                new() { ExerciseId = Guid.Parse("f3b5997b-8474-5e3c-b7f9-f9c02a006e0b"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("02b98b0d-7e10-542b-be14-a3e646ba1311"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("63c78d6b-fd44-5650-95a2-a029e242c9ec"), Groups = { new SetGroupTarget { Sets = "4", Reps = "8-12" } } },
            } });

        // circuit: Circuit Training
        library.Routines.Add(new RoutineDefinition { Id = Guid.Parse("7d08f018-59d7-5ff7-9aab-7e939ba478fa"), AccountId = Guid.Empty, OwnerMemberId = null,
            OwnerNameSnapshot = "Bowflex", Model = PlaceholderBowflexModel, Name = "Circuit Training", Visibility = Visibility.Manufacturer, Type = RoutineType.Standard,
            UpdatedAt = SeedTime, Exercises = new List<RoutineExerciseTarget> {
                new() { ExerciseId = Guid.Parse("c154b8b6-81dd-5531-8faf-7f5bd4bc6ca8"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("f0484f54-a3c0-5ade-a443-9941b074d0a8"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("291c7947-a8f4-5579-8f39-4bc532dd96eb"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("05cd28e4-7534-58b6-b2e0-7c895a81f516"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("f39d2682-2a58-593e-804e-d4bc788d2e54"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("7a9fc827-f764-59ba-b94b-a11e12f03d6b"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("74a41b00-8711-5a59-864d-70e02eb6e730"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("63c78d6b-fd44-5650-95a2-a029e242c9ec"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("18a09b84-0ec0-5491-ad50-dd981fc67194"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
                new() { ExerciseId = Guid.Parse("936eb155-cf1b-5f64-af3f-fb5791f02cd2"), Groups = { new SetGroupTarget { Sets = "1", Reps = "8-12" } } },
            } });

        // strength: Strength Training
        library.Routines.Add(new RoutineDefinition { Id = Guid.Parse("29d82027-6600-5b2b-b48f-9a87342363c6"), AccountId = Guid.Empty, OwnerMemberId = null,
            OwnerNameSnapshot = "Bowflex", Model = PlaceholderBowflexModel, Name = "Strength Training", Visibility = Visibility.Manufacturer, Type = RoutineType.Standard,
            UpdatedAt = SeedTime, Exercises = new List<RoutineExerciseTarget> {
                new() { ExerciseId = Guid.Parse("aa92ec77-c696-502a-93fe-0d5c43237e18"), Groups = { new SetGroupTarget { Sets = "4", Reps = "6-8" } } },
                new() { ExerciseId = Guid.Parse("3eb86938-0037-51d4-9421-dac0d988605e"), Groups = { new SetGroupTarget { Sets = "4", Reps = "6-8" } } },
                new() { ExerciseId = Guid.Parse("f39d2682-2a58-593e-804e-d4bc788d2e54"), Groups = { new SetGroupTarget { Sets = "4", Reps = "6-8" } } },
                new() { ExerciseId = Guid.Parse("05cd28e4-7534-58b6-b2e0-7c895a81f516"), Groups = { new SetGroupTarget { Sets = "3", Reps = "6-8" } } },
                new() { ExerciseId = Guid.Parse("a45666ab-fa3d-5835-9b92-e28aec260eb5"), Groups = { new SetGroupTarget { Sets = "3", Reps = "6-8" } } },
                new() { ExerciseId = Guid.Parse("74a41b00-8711-5a59-864d-70e02eb6e730"), Groups = { new SetGroupTarget { Sets = "3", Reps = "6-8" } } },
            } });

        return library;
    }
}