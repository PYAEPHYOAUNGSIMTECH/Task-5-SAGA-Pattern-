using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskItem>>> GetOrders()
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
