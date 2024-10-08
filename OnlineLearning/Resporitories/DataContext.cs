﻿using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OnlineLearning.Models;
using YourNamespace.Models;


namespace OnlineLearningApp.Respositories
{
    public class DataContext : IdentityDbContext<AppUserModel>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {

        }

		public DbSet<AppUserModel> Users { get; set; }
        public DbSet<InstructorModel> Instructors { get; set; }
        public DbSet<CourseModel> Courses { get; set; }
        public DbSet<CategoryModel> Category{ get; set; }
        public DbSet<InstructorConfirmationModel> InstructorConfirmation { get; set; }
        public DbSet<StudentCourseModel> StudentCourses { get; set; }
        public DbSet<ReviewMoel> Reviews { get; set; }
        public DbSet<CourseMaterialModel> CourseMaterials { get; set; }
        public DbSet<PaymentModel> Payment { get; set; }
        public DbSet<LectureModel> Lecture { get; set; }
        public DbSet<LectureFileModlel> LectureFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            SeedRoles(builder);
        }

       

        private void SeedRoles(ModelBuilder builder)
        {
            builder.Entity<IdentityRole>().HasData
            (
                new IdentityRole
                {
                    Id = "1",  // Role ID cho Admin
                    Name = "Admin",
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    NormalizedName = "ADMIN"
                },
                new IdentityRole
                {
                    Id = "2",  // Role ID cho Student
                    Name = "Student",
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    NormalizedName = "STUDENT"
                },
                new IdentityRole
                {
                    Id = "3",  // Role ID cho Instructor
                    Name = "Instructor",
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    NormalizedName = "INSTRUCTOR"
                }
            );
        }


    }
}
