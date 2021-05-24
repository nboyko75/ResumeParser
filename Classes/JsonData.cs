using System.Collections.Generic;

namespace ResumeParser.Classes
{
    public class JsonDataInfo
    {
        public static string[] SimpleClasses = { "Contact" };
        public static string[] ListClasses = { "Study", "Job", "Skill", "Project", "Award" };
        public static string[] MultipleValueProps = { "Description", "Biography", "Address", "Language", "Institution_Location",
            "Location", "Tools", "AwardedBy" };
        public static string[] DateProps = { "BirthDate", "StartDate", "EndDate" };
    }

    public class JsonData
    {
        public Contact contact { get; set; }
        public List<Study> studies { get; set; }
        public List<Job> jobs { get; set; }
        public List<Skill> skills { get; set; }
        public List<Project> projects { get; set; }
        public List<Award> awards { get; set; }

        public JsonData() 
        {
            contact = new Contact();
            studies = new List<Study>();
            jobs = new List<Job>();
            skills = new List<Skill>();
            projects = new List<Project>();
            awards = new List<Award>();
        }
    }

    public class Contact
    {
        public string Biography { get; set; }
        public string BirthDate { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string LinkedIn { get; set; }
        public string Github { get; set; }
        public string Language { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
    }

    public class Study
    {
        public string Institution { get; set; }
        public string Institution_Location { get; set; }
        public string Degree { get; set; }
        public string Gpa { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }

    public class Job
    {
        public string Company { get; set; }
        public string Location { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }

    public class Skill
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class Project 
    {
        public string Name { get; set; }
        public string Link { get; set; }
        public string Description { get; set; }
        public string Tools { get; set; }
    }

    public class Award
    {
        public string Name { get; set; }
        public string DateReceived { get; set; }
        public string AwardedBy { get; set; }
        public string Description { get; set; }
    }
}
