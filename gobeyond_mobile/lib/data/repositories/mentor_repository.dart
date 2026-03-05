import '../../core/network/dio_client.dart';

class MentorRepository {
  MentorRepository(this._client);

  final DioClient _client;

  Future<void> getMentors() async {
    await _client.dio.get('/api/mentors');
  }
}
