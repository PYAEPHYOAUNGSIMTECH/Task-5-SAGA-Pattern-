using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using taskAPI.Models;

namespace taskAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskProcessorController : ControllerBase
    {
        private readonly TaskItemDBContext _context;

        public TaskProcessorController(TaskItemDBContext context)
        {
            _context = context;
        }
        //SAGA Pattern
        //Task Processor Service will process the task and update an “Task Status”,  of a particular “Task ID” to [COMPLETED] or [FAILED] which will be published to “task-processed” queue.
        //will publish the task status to the task-processed
        [HttpPost]
        public async Task<string> PostTasks([FromBody] TaskItem taskItem)
        {
            //set the taskobj with the value from the frontend!
            TaskItem taskobj = new TaskItem();
            taskobj.taskItemID = taskItem.taskItemID;
            taskobj.description = taskItem.description;
            taskobj.status = taskItem.status;
            taskobj.priority = taskItem.priority;

            
        

            var factory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                Port = Convert.ToInt32(Environment.GetEnvironmentVariable("RABBITMQ_PORT"))
            };

            Console.WriteLine(factory.HostName + ":" + factory.Port);
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "task-processed",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);
                //serialize the task obj and publish it to the task-processed queue.
                string message = JsonConvert.SerializeObject(taskobj);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                                     routingKey: "task-processed",
                                     basicProperties: null,
                                     body: body);
            }


            //await _context.SaveChangesAsync();

            return JsonConvert.SerializeObject(taskobj);
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasks()
        {

            System.Diagnostics.Debug.WriteLine("Startiing consumer");
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "tasks",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (model, ea) =>
                {
                    System.Diagnostics.Debug.WriteLine("Entering", ea.Body);
                    var body = ea.Body.ToArray();
                    System.Diagnostics.Debug.WriteLine(body);
                    var message = Encoding.UTF8.GetString(body);
                    System.Diagnostics.Debug.WriteLine(" [x] Received {0}", message);
                    await Task.Yield();

                };
                System.Diagnostics.Debug.WriteLine("Starting consumer");
                channel.BasicConsume(queue: "tasks",
                                     autoAck: true,
                                     consumer: consumer);

            }
            return await _context.TaskItems.ToListAsync();
        }
    }
}
