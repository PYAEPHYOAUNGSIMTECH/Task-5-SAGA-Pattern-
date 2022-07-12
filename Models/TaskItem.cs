namespace taskAPI.Models
{
    public class TaskItem
    {
        public int taskItemID { get; set; }
        public String description { get; set; }

        public String priority { get; set; }

        public String status { get; set; }

        public int customerID { get; set; }
    }
}
