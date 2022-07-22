﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using taskAPI.Models;

namespace taskAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskItemsController : ControllerBase
    {
        private readonly TaskItemDBContext _context;

        public TaskItemsController(TaskItemDBContext context)
        {
            _context = context;
        }

        // GET: api/TaskItems
        // Task Service will consume the "task processed" event and update the Task Status for a given Task ID 
        // SAGA PATTERN
        // GET method to consume from "Task-processed Queue"
        [HttpGet]
        public async Task<ActionResult<List<String>>> GetTaskItems()
        {
            List<string> taskitems = new List<string>();

          if (_context.TaskItems == null)
          {
              return NotFound();
          }
            System.Diagnostics.Debug.WriteLine("Startiing consumer");
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                TaskItem task = new TaskItem();
                channel.QueueDeclare(queue: "task-processed",
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
                    taskitems.Add(message);
                    System.Diagnostics.Debug.WriteLine(" [x] Received {0}", message);
                    //store the message from the queue to task object.
                    task = JsonConvert.DeserializeObject<TaskItem>(message);

                    //store in the in-memory database
                    _context.Add(task);
                    await _context.SaveChangesAsync();

                };
                System.Diagnostics.Debug.WriteLine("Starting consumer");
                channel.BasicConsume(queue: "task-processed",
                                     autoAck: true,
                                     consumer: consumer);

            }
            if(taskitems.Count == 0)
            {
                return NoContent();
            }
            return taskitems;
        }

        // GET: api/TaskItems/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskItem>> GetTaskItem(int id)
        {
          if (_context.TaskItems == null)
          {
              return NotFound();
          }
            var taskItem = await _context.TaskItems.FindAsync(id);

            if (taskItem == null)
            {
                return NotFound();
            }

            return taskItem;
        }

        // PUT: api/TaskItems/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTaskItem(int id, TaskItem taskItem)
        {
            if (id != taskItem.taskItemID)
            {
                return BadRequest();
            }

            _context.Entry(taskItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/TaskItems
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TaskItem>> PostTaskItem([FromBody] TaskItem taskItem)
        {
           
            
          if (_context.TaskItems == null)
          {
              return Problem("Entity set 'TaskItemDBContext.TaskItems'  is null.");
          }
            _context.TaskItems.Add(taskItem);

            var factory = new ConnectionFactory()
            {
                HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                Port = Convert.ToInt32(Environment.GetEnvironmentVariable("RABBITMQ_PORT"))
            };

            Console.WriteLine(factory.HostName + ":" + factory.Port);
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "tasks",
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                string message = JsonConvert.SerializeObject(taskItem);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish(exchange: "",
                                     routingKey: "tasks",
                                     basicProperties: null,
                                     body: body);
            }


            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTaskItem", new { id = taskItem.taskItemID }, taskItem);
        }

        // DELETE: api/TaskItems/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTaskItem(int id)
        {
            if (_context.TaskItems == null)
            {
                return NotFound();
            }
            var taskItem = await _context.TaskItems.FindAsync(id);
            if (taskItem == null)
            {
                return NotFound();
            }

            _context.TaskItems.Remove(taskItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TaskItemExists(int id)
        {
            return (_context.TaskItems?.Any(e => e.taskItemID == id)).GetValueOrDefault();
        }
    }
}
