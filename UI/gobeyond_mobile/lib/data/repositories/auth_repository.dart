import '../../core/network/dio_client.dart';

class AuthRepository {
  AuthRepository(this._client);

  final DioClient _client;

  Future<void> login(String email, String password) async {
    await _client.dio.post('/api/auth/login', data: {'email': email, 'password': password});
  }
}
