using SQLite;
using GestionEscuelaEspana.Models;
using System.IO;

namespace GestionEscuelaEspana.Data
{
    public class DatabaseService
    {
        private SQLiteConnection? _db;

        public void Inicializar()
        {
            if (_db != null) return;

            // Ruta local nativa según el sistema operativo (Windows/Android/macOS)
            var rutaBase = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var rutaDb = Path.Combine(rutaBase, "escuela_espana.db3");

            _db = new SQLiteConnection(rutaDb);
            _db.CreateTable<Instructor>();
            _db.CreateTable<Cita>();
        }

        // --- MÉTODOS INSTRUCTORES ---
        public List<Instructor> GetInstructores() => _db!.Table<Instructor>().ToList();
        public void GuardarInstructor(Instructor inst)
        {
            if (inst.Id == 0)
            {
                _db!.Insert(inst); // Si el ID es 0, inserta uno nuevo autoincrementable
            }
            else
            {
                _db!.Update(inst); // Si ya tiene ID, actualiza el existente
            }
        }


        public void EliminarInstructor(int id) => _db!.Delete<Instructor>(id);

        
        public List<Cita> GetCitas()
        {
            var citas = _db!.Table<Cita>().ToList();
            var instructores = GetInstructores().ToDictionary(i => i.Id, i => i.Nombre);

            foreach (var cita in citas)
            {
                if (instructores.TryGetValue(cita.InstructorId, out var nombreInst))
                {
                    cita.InstructorNombre = nombreInst;
                }
            }
            return citas;
        }
        public void GuardarCita(Cita cita)
        {
            if (cita.Id == 0)
            {
                _db!.Insert(cita); // Si el ID es 0, inserta una nueva cita autoincrementable
            }
            else
            {
                _db!.Update(cita); // Si ya tiene ID, actualiza la existente
            }
        }

        public void EliminarCita(int id) => _db!.Delete<Cita>(id);
    }
}