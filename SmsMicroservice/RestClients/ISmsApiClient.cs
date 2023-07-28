namespace SmsMicroservice.RestClients;

public interface ISmsApiClient<T, TResult>
{
  
        Task<TResult> PostAsync(T item);
 
}