using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLayer.DTOs;

namespace BusinessLayer.Interfaces
{
    public interface IBenchmarkService
    {
        // Experiments
        Task<IEnumerable<ExperimentDto>> GetAllExperimentsAsync();
        Task<ExperimentDto?> GetExperimentByIdAsync(int id);
        Task<ExperimentDto> CreateExperimentAsync(ExperimentDto experimentDto);
        Task UpdateExperimentAsync(ExperimentDto experimentDto);
        Task DeleteExperimentAsync(int id);

        // Test Sets (Questions & Ground Truth)
        Task<IEnumerable<TestSetDto>> GetTestSetsBySubjectIdAsync(int subjectId);
        Task<TestSetDto?> GetTestSetByIdAsync(int id);
        Task<TestSetDto> CreateTestSetAsync(TestSetDto testSetDto);
        Task UpdateTestSetAsync(TestSetDto testSetDto);
        Task DeleteTestSetAsync(int id);

        // AI Models
        Task<IEnumerable<AimodelDto>> GetAllAIModelsAsync();

        // Running Benchmarks
        Task<IEnumerable<BenchmarkResultDto>> RunBenchmarkAsync(int experimentId, int subjectId);
        Task<IEnumerable<BenchmarkResultDto>> GetResultsByExperimentIdAsync(int experimentId);
        Task<IEnumerable<BenchmarkResultDto>> GetAllResultsAsync();
    }
}
