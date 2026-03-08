import 'package:flutter_bloc/flutter_bloc.dart';

class SubscriptionState {
  const SubscriptionState();
}

class SubscriptionBloc extends Cubit<SubscriptionState> {
  SubscriptionBloc() : super(const SubscriptionState());
}
