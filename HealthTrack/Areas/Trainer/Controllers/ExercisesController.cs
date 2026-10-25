using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using HealthTrack.Models;
using HealthTrack.Helpers;
using HealthTrack.Areas.Trainer.ViewModels;

namespace HealthTrack.Areas.Trainer.Controllers
{
    [TrainerAuthorize]
    public class ExercisesController : Controller
    {
        private readonly HealthTrackEntities db = new HealthTrackEntities();

        private static bool IsAllOrEmpty(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var trimmed = value.Trim();
            return trimmed.Equals("all", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("all ", StringComparison.OrdinalIgnoreCase);
        }

        // 1. GET: /Exercises
        // Comprehensive Exercise Library with search and multi-facet filters
        [HttpGet]
        public ActionResult Index(string search, string category, string difficulty, string muscle, string equipment)
        {
            var allExercises = db.Exercises.OrderBy(e => e.Name).ToList();

            var query = db.Exercises.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(e =>
                    e.Name.Contains(term)
                    || e.Description.Contains(term)
                    || e.TargetMuscle.Contains(term)
                    || (e.SecondaryMuscles != null && e.SecondaryMuscles.Contains(term))
                    || e.Equipment.Contains(term)
                    || e.Category.Contains(term)
                    || e.DifficultyLevel.Contains(term));
            }

            if (!IsAllOrEmpty(category))
            {
                var cat = category.Trim();
                query = query.Where(e => e.Category == cat);
            }

            if (!IsAllOrEmpty(difficulty))
            {
                var diff = difficulty.Trim();
                query = query.Where(e => e.DifficultyLevel == diff);
            }

            if (!IsAllOrEmpty(muscle))
            {
                var m = muscle.Trim();
                query = query.Where(e => e.TargetMuscle == m || (e.SecondaryMuscles != null && e.SecondaryMuscles.Contains(m)));
            }

            if (!IsAllOrEmpty(equipment))
            {
                var eq = equipment.Trim();
                query = query.Where(e => e.Equipment == eq);
            }

            var exercises = query.OrderBy(e => e.Name).ToList();

            var viewModel = new ExerciseLibraryViewModel
            {
                Exercises = exercises,
                AllExercises = allExercises,
                SearchTerm = search,
                SelectedCategory = category,
                SelectedDifficulty = difficulty,
                SelectedMuscle = muscle,
                SelectedEquipment = equipment
            };

            return View(viewModel);
        }

        // 2. GET: /Exercises/Details/5
        // Comprehensive breakdown of execution steps, form cues, and muscle involvement
        [HttpGet]
        public ActionResult Details(int id)
        {
            var exercise = db.Exercises.Find(id);
            if (exercise == null)
            {
                TempData["ErrorMessage"] = "The requested exercise was not found.";
                return RedirectToAction("Index");
            }

            return View(exercise);
        }

        // 3. GET: /Exercises/Create
        // Trainer form to add a new reusable exercise to the library
        [HttpGet]
        public ActionResult Create()
        {
            var exercise = new Exercises();
            return View(exercise);
        }

        // 4. POST: /Exercises/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Exercises exercise, System.Web.HttpPostedFileBase imageFile, System.Web.HttpPostedFileBase startImageFile, System.Web.HttpPostedFileBase movementImageFile)
        {
            if (!ModelState.IsValid)
            {
                return View(exercise);
            }

            try
            {
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    exercise.ImagePath = SaveUploadedFile(imageFile, "ex");
                }

                if (startImageFile != null && startImageFile.ContentLength > 0)
                {
                    exercise.StartImagePath = SaveUploadedFile(startImageFile, "start");
                }

                if (movementImageFile != null && movementImageFile.ContentLength > 0)
                {
                    exercise.MovementImagePath = SaveUploadedFile(movementImageFile, "move");
                }

                exercise.CreatedAt = DateTime.UtcNow;
                db.Exercises.Add(exercise);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Exercise added to the library successfully.";
                return RedirectToAction("Index");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException)
            {
                ModelState.AddModelError(
                    "",
                    "We couldn't save this exercise because some information is invalid. Please review your details and try again."
                );

                return View(exercise);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "An unexpected error occurred while saving the exercise. Please try again."
                );

                return View(exercise);
            }
        }

        // 5. GET: /Exercises/Edit/5
        [HttpGet]
        public ActionResult Edit(int id)
        {
            var exercise = db.Exercises.Find(id);
            if (exercise == null)
            {
                TempData["ErrorMessage"] = "Exercise not found.";
                return RedirectToAction("Index");
            }

            return View(exercise);
        }

        // 6. POST: /Exercises/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Exercises exercise, System.Web.HttpPostedFileBase imageFile, System.Web.HttpPostedFileBase startImageFile, System.Web.HttpPostedFileBase movementImageFile)
        {
            if (!ModelState.IsValid)
            {
                return View(exercise);
            }

            var entity = db.Exercises.Find(exercise.ExerciseId);
            if (entity == null)
            {
                TempData["ErrorMessage"] = "Failed to update exercise.";
                return RedirectToAction("Index");
            }

            entity.Name = exercise.Name;
            entity.Category = exercise.Category;
            entity.DifficultyLevel = exercise.DifficultyLevel;
            entity.TargetMuscle = exercise.TargetMuscle;
            entity.SecondaryMuscles = exercise.SecondaryMuscles;
            entity.Equipment = exercise.Equipment;
            entity.Description = exercise.Description;
            entity.Instructions = exercise.Instructions;
            entity.TipsAndPrecautions = exercise.TipsAndPrecautions;
            entity.DefaultCaloriesBurnEstimate = exercise.DefaultCaloriesBurnEstimate;
            entity.MovementPattern = exercise.MovementPattern;
            entity.KeyGuidance = exercise.KeyGuidance;

            if (imageFile != null && imageFile.ContentLength > 0)
            {
                var uploaded = SaveUploadedFile(imageFile, "ex");
                if (!string.IsNullOrEmpty(uploaded)) entity.ImagePath = uploaded;
            }

            if (startImageFile != null && startImageFile.ContentLength > 0)
            {
                var uploaded = SaveUploadedFile(startImageFile, "start");
                if (!string.IsNullOrEmpty(uploaded)) entity.StartImagePath = uploaded;
            }

            if (movementImageFile != null && movementImageFile.ContentLength > 0)
            {
                var uploaded = SaveUploadedFile(movementImageFile, "move");
                if (!string.IsNullOrEmpty(uploaded)) entity.MovementImagePath = uploaded;
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = $"Exercise '{exercise.Name}' was updated successfully!";
            return RedirectToAction("Details", new { id = exercise.ExerciseId });
        }

        private string SaveUploadedFile(System.Web.HttpPostedFileBase file, string prefix)
        {
            if (file == null || file.ContentLength == 0) return null;

            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = System.IO.Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext)) return null;

            // 5MB limit
            if (file.ContentLength > 5 * 1024 * 1024) return null;

            var folder = Server.MapPath("~/Content/images/exercises/");
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            var uniqueName = $"{prefix}_{DateTime.UtcNow.Ticks}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}";
            var physicalPath = System.IO.Path.Combine(folder, uniqueName);
            file.SaveAs(physicalPath);

            return $"~/Content/images/exercises/{uniqueName}";
        }

        // 7. POST: /Exercises/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var exercise = db.Exercises
                .Include(e => e.WorkoutPlanExercises)
                .FirstOrDefault(e => e.ExerciseId == id);

            if (exercise != null)
            {
                db.WorkoutPlanExercises.RemoveRange(exercise.WorkoutPlanExercises);
                db.Exercises.Remove(exercise);
                db.SaveChanges();
                TempData["SuccessMessage"] = $"Exercise '{exercise.Name}' was removed from the library.";
            }

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
