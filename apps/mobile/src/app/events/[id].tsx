import { useLocalSearchParams } from "expo-router";
import EventScreen from "../../screens/EventScreen";

export default function EventRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <EventScreen eventId={id} />;
}
