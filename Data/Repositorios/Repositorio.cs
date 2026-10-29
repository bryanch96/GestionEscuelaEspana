using System.Linq.Expressions;
using GestionEscuelaEspana.Models;
using SQLite;

namespace GestionEscuelaEspana.Data.Repositorios
{
    /// <summary>Operaciones CRUD genéricas sobre una tabla SQLite.</summary>
    public interface IRepositorio<T> where T : class, IEntidad, new()
    {
        Task<List<T>> ListarAsync(Expression<Func<T, bool>>? filtro = null);
        Task<T?> ObtenerAsync(int id);
        Task<T?> PrimeroAsync(Expression<Func<T, bool>> filtro);
        Task<int> ContarAsync(Expression<Func<T, bool>>? filtro = null);

        /// <summary>Inserta si la entidad es nueva (Id = 0) o actualiza si ya existe.</summary>
        Task GuardarAsync(T entidad);
        Task EliminarAsync(int id);

        /// <summary>Consulta componible para filtros avanzados (se traduce a SQL).</summary>
        Task<AsyncTableQuery<T>> ConsultaAsync();
    }

    public class Repositorio<T> : IRepositorio<T> where T : class, IEntidad, new()
    {
        protected readonly AppDatabase Database;

        public Repositorio(AppDatabase database) => Database = database;

        public async Task<AsyncTableQuery<T>> ConsultaAsync() => (await Database.ObtenerConexionAsync()).Table<T>();

        public async Task<List<T>> ListarAsync(Expression<Func<T, bool>>? filtro = null)
        {
            var consulta = await ConsultaAsync();
            return filtro == null ? await consulta.ToListAsync() : await consulta.Where(filtro).ToListAsync();
        }

        public async Task<T?> ObtenerAsync(int id)
        {
            var db = await Database.ObtenerConexionAsync();
            return await db.FindAsync<T>(id);
        }

        public async Task<T?> PrimeroAsync(Expression<Func<T, bool>> filtro)
        {
            var consulta = await ConsultaAsync();
            return await consulta.Where(filtro).FirstOrDefaultAsync();
        }

        public async Task<int> ContarAsync(Expression<Func<T, bool>>? filtro = null)
        {
            var consulta = await ConsultaAsync();
            return filtro == null ? await consulta.CountAsync() : await consulta.Where(filtro).CountAsync();
        }

        public async Task GuardarAsync(T entidad)
        {
            var db = await Database.ObtenerConexionAsync();
            if (entidad.Id == 0)
                await db.InsertAsync(entidad);
            else
                await db.UpdateAsync(entidad);
        }

        public async Task EliminarAsync(int id)
        {
            var db = await Database.ObtenerConexionAsync();
            await db.DeleteAsync<T>(id);
        }
    }
}
