﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Models;
using OnlineLearningApp.Respositories;

namespace OnlineLearning.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]

    [Route("Admin/[controller]/[action]")]
    public class CourseController : Controller
    {
        private UserManager<AppUserModel> _userManager;
        private SignInManager<AppUserModel> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly DataContext _dataContext;
        public CourseController(DataContext dataContext, UserManager<AppUserModel> userManager, SignInManager<AppUserModel> signInManager, IWebHostEnvironment webHostEnvironment)
        {
            _dataContext = dataContext;
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
        }
        public async Task<IActionResult> Index()
        {
            var courses = await _dataContext.Courses.Include(c => c.Instructor).ThenInclude(i => i.AppUser).ToListAsync();
            return View(courses);
        }

        public async Task<IActionResult> SetStatusCourse(int Id)
        {
            var course = await _dataContext.Courses.FindAsync(Id);
            if (course == null)
            {
                TempData["error"] = "Course Not Found";
                return RedirectToAction("Index", "Course");
            }
            if (course.Status == true)
            {
                course.Status = false;
                await _dataContext.SaveChangesAsync();
            }
            else if (course.Status == false)
            {

                course.Status = true;
                await _dataContext.SaveChangesAsync();
            }
                TempData["success"] = "Set Status Successful!";
                return RedirectToAction("Index", "Course");
            }

        }
    }

