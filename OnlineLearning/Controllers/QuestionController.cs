﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging.Signing;
using OfficeOpenXml;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Text;
using OnlineLearning.Models;
using OnlineLearning.Models.ViewModel;
using OnlineLearningApp.Respositories;
using System.Security.Claims;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace OnlineLearning.Controllers
{
    [Authorize]
    public class QuestionController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DataContext datacontext;
        private UserManager<AppUserModel> _userManager;
        private SignInManager<AppUserModel> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public QuestionController(ILogger<HomeController> logger, DataContext context, SignInManager<AppUserModel> signInManager, UserManager<AppUserModel> userManager, IWebHostEnvironment webHostEnvironment)
        {
            datacontext = context;
            _logger = logger;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
        }
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "Instructor")]
        public IActionResult CreateQuestionRedirector(int TestID)
        {
            ViewBag.TestID = TestID;
            TestModel test = datacontext.Test.Find(TestID);
            ViewBag.CourseID = test.CourseID;
            ViewBag.Test = test;
            return View("CreateQuestion");
        }

        [Authorize(Roles = "Instructor")]
        public IActionResult EditQuestionRedirector(int TestID)
        {
            ViewBag.TestID = TestID;
            TestModel test = datacontext.Test.Find(TestID);
            ViewBag.CourseID = test.CourseID;
            ViewBag.Test = test;
            List<QuestionModel> list = datacontext.Question
                .Where(t => t.TestID == TestID)
                .ToList();
            return View("ViewQuestionsToEdit", list);
        }

        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> CreateQuestion(QuestionViewModel model)
        {
            if (model == null)
            {
                TempData["Error"] = "You haven't enter anything";
                return View();
            }
            //it wont be null, pls microsoft, my ocd
            model.Test = datacontext.Test.Find(model.TestID);
            if (model.Test == null)
            {
                TempData["Error"] = "Test Not Found";
                return View();
            }
            var newQuestion = new QuestionModel
            {
                AnswerA = model.AnswerA,
                AnswerB = model.AnswerB,
                AnswerC = model.AnswerC,
                AnswerD = model.AnswerD,
                CorrectAnswer = model.CorrectAnswer,
                TestID = model.TestID,
                Test = model.Test,
                QuestionID = model.QuestionID,
                Question = model.QuestionText,
                
                //  ImagePath=model.ImagePath, null for now
            };
            if (model.QuestionImage != null)
            {
                string uploadpath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "QuestionImages");
                string imagename = Guid.NewGuid() + "_" + model.QuestionImage.FileName;
                string filepath = Path.Combine(uploadpath, imagename);

                using (var fs = new FileStream(filepath, FileMode.Create))
                {
                    await model.QuestionImage.CopyToAsync(fs);
                }
                newQuestion.ImagePath = imagename;
            }
            else
            {
                newQuestion.ImagePath = "";
            }
            
            datacontext.Question.Add(newQuestion);

            var numberOfQuestions = datacontext.Question.ToList()
                .Where(q => q.TestID == model.TestID)
                .Count();

            model.Test.NumberOfQuestion = numberOfQuestions;

            datacontext.Test.Update(model.Test);
            datacontext.SaveChanges();
            TempData["success"] = "Course created successfully!";

            //ViewBag.CourseID = model.CourseID;
            //ViewBag.TestID = model.TestID;
            //TestModel test = datacontext.Test.Find(model.TestID);
            //ViewBag.Test = test;

            //return back to the redirector for another quesiton to add
            return RedirectToAction("CreateQuestionRedirector", new { testID = model.TestID });
        }

        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> ImportExcel(IFormFile ExcelFile, bool skipFirstLine, int TestID)
        {
            // Set the license context to non-commercial use
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            if (ExcelFile == null || ExcelFile.Length == 0)
            {
                TempData["Error"] = "Please upload a valid Excel file.";
                return View("CreateQuestion");
            }
            // Get the file extension
            var extension = Path.GetExtension(ExcelFile.FileName).ToLowerInvariant();

            // If skipFirstLine is true, start from row 2 (index 1), otherwise start from row 1
            int startRow;
            if (skipFirstLine)
            {
                startRow = 2;
            }
            else
            {
                startRow = 1;
            }
            if (extension == ".csv")
            {
                TempData["Error"] = "You should use the Import CSV for this file";
                return RedirectToAction("CreateQuestionRedirector", new { testID = TestID });
            }
            else if (extension != ".xlsx" && extension != ".xls")
            {
                TempData["Error"] = "Unsupported file format. Please upload a Excel file.";
                return RedirectToAction("CreateQuestionRedirector", new { testID = TestID });
            }
            List<QuestionModel> questions = new List<QuestionModel>();
            var test = await datacontext.Test.FindAsync(TestID);
            // Use EPPlus to read the Excel file
            using (var package = new OfficeOpenXml.ExcelPackage(ExcelFile.OpenReadStream()))
            {
                var worksheet = package.Workbook.Worksheets[0]; // Assuming the first sheet

                int rowCount = worksheet.Dimension.Rows;

                for (int row = startRow; row <= rowCount; row++)
                {
                    // Read the values from the Excel file (assuming the format is the same)
                    //string testIDStr = worksheet.Cells[row, 1].Text.Trim();
                    string questionText = worksheet.Cells[row, 1].Text.Trim();
                    string answerA = worksheet.Cells[row, 2].Text.Trim();
                    string answerB = worksheet.Cells[row, 3].Text.Trim();
                    string answerC = worksheet.Cells[row, 4].Text.Trim();
                    string answerD = worksheet.Cells[row, 5].Text.Trim();
                    string correctAnswer = worksheet.Cells[row, 6].Text.Trim().ToUpper();

                    // Validate that each row has the correct number of columns
                    if (string.IsNullOrEmpty(questionText) ||
                        string.IsNullOrEmpty(answerA) || string.IsNullOrEmpty(answerB) ||
                        string.IsNullOrEmpty(answerC) || string.IsNullOrEmpty(answerD) ||
                        string.IsNullOrEmpty(correctAnswer))
                    {
                        TempData["Error"] = $"Row {row} contains invalid data. Ensure all columns are filled.";
                        break;
                    }

                    // Validate TestID
                    //!!!Reserve for multiple upload test, pls dont delete
                    //if (!int.TryParse(testIDStr, out int testID))
                    //{
                    //    TempData["Error"] = $"Invalid TestID '{testIDStr}' in row {row}.";
                    //    break;
                    //}
                    //if (await CheckValidTestToInstructor(testID) == false)
                    //{
                    //    TempData["Error"] = "You do not have permision to do this";
                    //    break;
                    //}
                    // Validate correct answer
                    if (!new[] { "A", "B", "C", "D" }.Contains(correctAnswer))
                    {
                        TempData["Error"] = $"Invalid correct answer '{correctAnswer}' in row {row}. Must be A, B, C, or D.";
                        break;
                    }

                    // Create a new QuestionModel object
                    var question = new QuestionModel
                    {
                        TestID = TestID,
                        Question = questionText,
                        AnswerA = answerA,
                        AnswerB = answerB,
                        AnswerC = answerC,
                        AnswerD = answerD,
                        CorrectAnswer = correctAnswer,
                        Test = test,
                        ImagePath = "" // Update this if you're also processing images
                    };
                    ViewBag.CourseID = test.CourseID;
                    questions.Add(question);
                }
            }

            // Save all the questions to the database
            datacontext.Question.AddRange(questions);

            var numberOfQuestions = datacontext.Question.ToList()
                .Where(q => q.TestID == TestID)
                .Count();

             test.NumberOfQuestion = numberOfQuestions;

            datacontext.Test.Update(test);
            await datacontext.SaveChangesAsync();

            TempData["Success"] = "Excel file processed successfully!";
            return RedirectToAction("CreateQuestionRedirector", new { testID = TestID });
        }

        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> ImportCSV(IFormFile CSVFile, bool skipFirstLine, int TestID)
        {
            var test = await datacontext.Test.FindAsync(TestID);
            if (CSVFile == null || CSVFile.Length == 0)
            {
                TempData["Error"] = "Please upload a valid CSV file.";
                return View("CreateQuestion");
            }
            // Get the file extension
            var extension = Path.GetExtension(CSVFile.FileName).ToLowerInvariant();
            if (extension == ".xlsx" || extension == ".xls")
            {
                TempData["Error"] = "You should use the Import Excel for this file";
                return View("CreateQuestion");
            }
            else if (extension != ".csv")
            {
                TempData["Error"] = "Unsupported file format. Please upload a CSV file.";
                return View("CreateQuestion");
            }
            List<QuestionModel> questions = new List<QuestionModel>();
            using (var reader = new StreamReader(CSVFile.OpenReadStream()))
            {
                string line;
                bool isFirstLine = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (isFirstLine && skipFirstLine)
                    {
                        isFirstLine = false;
                        continue;  // Skip the first line if the checkbox was checked
                    }

                    var values = line.Split(',');

                    if (values.Length != 6)
                    {
                        TempData["Error"] = "Each row must have exactly 6 columns (Question, Answer A, Answer B, Answer C, Answer D, CorrectAnswer).";
                        break;
                    }

                    // Parse the CSV values into variables
                    //!!!!Reserve for the upload for multiple test pls dont delete
                    //int testID;
                    //if (!int.TryParse(values[0], out testID))
                    //{
                    //    TempData["Error"] = "Invalid TestID in the CSV file.";
                    //    break;
                    //}
                    //if (await CheckValidTestToInstructor(TestID) == false)
                    //{
                    //    TempData["Error"] = "You do not have permision to do this";
                    //    break;
                    //}

                    if (test == null)
                    {
                        TempData["Error"] = $"Test with {TestID} does not exist";
                        break;
                    }
                    string questionText = values[0];
                    string answerA = values[1];
                    string answerB = values[2];
                    string answerC = values[3];
                    string answerD = values[4];
                    string correctAnswer = values[5].ToUpper();

                    // Validate correct answer
                    if (!new[] { "A", "B", "C", "D" }.Contains(correctAnswer))
                    {
                        TempData["Error"] = $"Invalid correct answer '{correctAnswer}' in the CSV file. Must be A, B, C, or D.";
                        break;
                    }

                    // Here, you can create a new Question object (assuming you have a Question model)
                    var question = new QuestionModel
                    {
                        TestID = TestID,
                        Question = questionText,
                        AnswerA = answerA,
                        AnswerB = answerB,
                        AnswerC = answerC,
                        AnswerD = answerD,
                        CorrectAnswer = correctAnswer,
                        Test = test,
                        ImagePath = ""
                    };
                    questions.Add(question);
                }
            }
            datacontext.Question.AddRange(questions);

            var numberOfQuestions = datacontext.Question.ToList()
                .Where(q => q.TestID == TestID)
                .Count();

            test.NumberOfQuestion = numberOfQuestions;

            datacontext.Test.Update(test);

            datacontext.SaveChanges();
            ViewBag.CourseID = test.CourseID;

            TempData["Success"] = "CSV file processed successfully!";
            return RedirectToAction("CreateQuestionRedirector", new { testID = TestID }); // Redirect back to the CreateQuestion page
        }
        // GET: Edit a specific question 
        // Act as a redirector, actual things are in the post method below
        //QuestionID for abstraction 
        [HttpGet]
        [Authorize(Roles = "Instructor")]
        public IActionResult EditQuestion(int QuestionID, int TestID)
        {
            // Retrieve the question from the database
            var question = datacontext.Question.FirstOrDefault(q => q.QuestionID == QuestionID && q.TestID == TestID);
            if (question == null)
            {
                return NotFound();
            }

            // Retrieve the test and set up ViewBag for Test and CourseID
            var test = datacontext.Test.Find(TestID);   

            var numberOfQuestions = datacontext.Question.ToList()
                .Where(q => q.TestID == TestID)
                .Count();
            test.NumberOfQuestion = numberOfQuestions;
            ViewBag.CourseID = test.CourseID;
           // ViewBag.TestID = TestID;

            //not sure if necessary
            ViewBag.QuestionID = QuestionID;
            // Populate the QuestionViewModel with the existing question data
            var questionViewModel = new QuestionViewModel
            {
                QuestionID = question.QuestionID,
                TestID = test.TestID,
                Test = test,
                AnswerA = question.AnswerA,
                AnswerB = question.AnswerB,
                AnswerC = question.AnswerC,
                AnswerD = question.AnswerD,
                CorrectAnswer = question.CorrectAnswer,
                CourseID = test.CourseID,
                ImagePath = question.ImagePath,  // Add the existing image path here
                QuestionText = question.Question,
                // Leave QuestionImage null, as no file is uploaded yet
            };
            datacontext.Test.Update(test);
            datacontext.SaveChanges();
            // Return the view with the pre-filled model
            return View(questionViewModel); 
        }

        // POST: Update the question
        [HttpPost]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> EditQuestion(QuestionViewModel model)
        {
            // Retrieve the question from the database
            var question = datacontext.Question.FirstOrDefault(q => q.QuestionID == model.QuestionID
            && q.TestID == model.TestID);

            if (question != null)
            {
                // Update the question details
                question.Question = model.QuestionText;
                question.AnswerA = model.AnswerA;
                question.AnswerB = model.AnswerB;
                question.AnswerC = model.AnswerC;
                question.AnswerD = model.AnswerD;
                question.CorrectAnswer = model.CorrectAnswer;

                // Handle image update
                if (model.QuestionImage != null)
                {
                    // Get the current image path
                    string oldImagePath = question.ImagePath;

                    // If there's an existing image, delete it
                    if (!string.IsNullOrEmpty(oldImagePath))
                    {
                        string oldImageFullPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "QuestionImages", oldImagePath);
                        if (System.IO.File.Exists(oldImageFullPath))
                        {
                            System.IO.File.Delete(oldImageFullPath);
                        }
                    }

                    // Save the new image
                    string uploadPath = Path.Combine(_webHostEnvironment.WebRootPath, "Images", "QuestionImages");
                    string imageName = Guid.NewGuid() + "_" + model.QuestionImage.FileName;
                    string filePath = Path.Combine(uploadPath, imageName);

                    using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        await model.QuestionImage.CopyToAsync(fs);
                    }

                    // Update the image path in the database
                    question.ImagePath = imageName;
                }

                // Update the question in the database
                datacontext.Update(question);
                await datacontext.SaveChangesAsync();

                TempData["Success"] = "Question updated successfully!";
            }
            else
            {
                TempData["Error"] = "Question not found.";
            }

             return RedirectToAction("EditQuestionRedirector", new {TestID = model.TestID });
        }

        /// <summary>
        /// Check if the instructor have permision from a file 
        /// !!!Reserve for the upload for multiple test, do not delete pls
        /// </summary>
        /// <param name="TestID"></param>
        /// <returns></returns>
        [Authorize(Roles = "Instructor")]
        private async Task<bool> CheckValidTestToInstructor(int TestID)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Test = await datacontext.Test.FindAsync(TestID);
            if (Test == null)
            {
                return false;
            }
            var Course = Test.Course;
            if (Course == null)
            {
                var dbCourse = await datacontext.Courses.FindAsync(Test.CourseID);
                if (dbCourse == null)
                {
                    return false;
                }
                else
                {
                    Course = dbCourse;
                }
            }
            if (userId == Course.InstructorID || userId.Equals(Course.InstructorID))
            {
                return true;
            }
            return false;
        }
    }
}
